using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PixelPay.Application.Common;
using PixelPay.Application.Notifications;
using PixelPay.Application.Promotions;
using PixelPay.Application.Wallets;
using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Orders;

/// <summary>Fulfils one order against its providers in priority order (queue "topup").</summary>
public sealed class TopupJob(
    IAppDbContext db, WalletService wallet, ITopupProviderFactory providers, CashbackService cashback,
    AffiliatesService affiliates, NotificationsService notifications, IOrderStatusNotifier live, ILogger<TopupJob> log)
{
    public async Task RunAsync(string orderId, string userId, string gameProductId, CancellationToken ct)
    {
        // Claim: only one worker (and never a cancelled order) gets past this.
        var now = PrismaTime.Now();
        var claimed = await db.Orders.Where(o => o.Id == orderId && o.Status == OrderStatus.PENDING)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.PROCESSING).SetProperty(o => o.UpdatedAt, now), ct);
        if (claimed == 0)
            return;

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId, ct);
        await live.NotifyAsync(userId, orderId, "PROCESSING", ct);

        var candidates = await db.ProviderProducts.AsNoTracking().Include(pp => pp.Provider)
            .Where(pp => pp.GameProductId == gameProductId && pp.IsAvailable && pp.Provider.IsActive)
            .OrderBy(pp => pp.Provider.Priority)
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            await FailAsync(orderId, userId, order.TotalPrice, "No providers available", ct);
            return;
        }

        var lastError = "";
        foreach (var pp in candidates)
        {
            TopupResult result;
            try
            {
                var adapter = providers.Create(new ProviderCredentials(pp.Provider.Slug, pp.Provider.ApiUrl,
                    pp.Provider.ApiKeyEnc, pp.Provider.ApiSecretEnc));
                result = await adapter.ProcessTopupAsync(new TopupRequest(pp.ProviderSku, order.GameUid, order.GameServer,
                    order.GameUsername, order.Id, order.Quantity), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new TopupResult(false, FailureReason: ex.Message);
                log.LogError(ex, "Exception from {Provider} for order {OrderId}", pp.Provider.Slug, orderId);
            }

            if (result.Success)
            {
                await CompleteAsync(order.Id, userId, order.TotalPrice, pp.ProviderId, result, ct);
                await AfterCompletionAsync(order, gameProductId, userId, ct);
                await live.NotifyAsync(userId, orderId, "COMPLETED", ct);
                log.LogInformation("Order {OrderId} completed via {Provider}", orderId, pp.Provider.Slug);
                return;
            }

            lastError = result.FailureReason ?? "Provider returned failure";
            log.LogWarning("Provider {Provider} failed for order {OrderId}: {Reason}", pp.Provider.Slug, orderId, lastError);
            await db.Orders.Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.RetryCount, o => o.RetryCount + 1), ct);
        }

        await FailAsync(orderId, userId, order.TotalPrice, lastError, ct);
    }

    private Task CompleteAsync(string orderId, string userId, decimal total, string providerId, TopupResult result,
        CancellationToken ct) => Tx.RunAsync(db, async () =>
    {
        var now = PrismaTime.Now();
        await db.Orders.Where(o => o.Id == orderId).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.Status, OrderStatus.COMPLETED)
            .SetProperty(o => o.ProviderId, providerId)
            .SetProperty(o => o.ProviderOrderId, result.ProviderOrderId)
            .SetProperty(o => o.ProviderReference, result.ProviderReference)
            .SetProperty(o => o.CompletedAt, now)
            .SetProperty(o => o.UpdatedAt, now), ct);
        var walletId = await wallet.WalletIdForUserAsync(userId, ct);
        await wallet.DebitLockedAsync(walletId, total, "Topup completed", orderId, "ORDER", ct);
    }, ct);

    /// <summary>Cashback, affiliate commission and the email: each is best-effort and never undoes the completed order.</summary>
    private async Task AfterCompletionAsync(Domain.Entities.Order order, string gameProductId, string userId, CancellationToken ct)
    {
        var product = await db.GameProducts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == gameProductId, ct);

        await BestEffort("Cashback credit", order.Id, async () =>
        {
            if (product is not null)
                await cashback.EvaluateAndCreditAsync(userId, order.Id, order.TotalPrice, product.GameId, ct);
        });

        await BestEffort("Affiliate commission", order.Id, async () =>
        {
            var referrerId = await db.Users.Where(u => u.Id == userId).Select(u => u.ReferredBy).FirstOrDefaultAsync(ct);
            if (referrerId is not null)
                await affiliates.AwardCommissionAsync(referrerId, order.Id, order.TotalPrice, ct);
        });

        await BestEffort("Notification send", order.Id, () => notifications.SendAsync(new SendNotification(
            userId, "ORDER_COMPLETED", NotificationChannel.EMAIL, "Order Completed",
            $"Your order {order.OrderNumber} has been completed.",
            new Dictionary<string, object?>
            {
                ["orderNumber"] = order.OrderNumber,
                ["productName"] = product?.Name,
                ["gameUid"] = order.GameUid,
            }), ct));
    }

    private async Task FailAsync(string orderId, string userId, decimal total, string reason, CancellationToken ct)
    {
        await Tx.RunAsync(db, async () =>
        {
            // Tracked update: Pomelo cannot bind a JSON column parameter inside ExecuteUpdate.
            var failed = await db.Orders.SingleAsync(o => o.Id == orderId, ct);
            failed.Status = OrderStatus.FAILED;
            failed.Metadata = new RawJson(JsonSerializer.Serialize(new { failureReason = reason }));
            await db.SaveChangesAsync(ct);
            var walletId = await wallet.WalletIdForUserAsync(userId, ct);
            await wallet.UnlockAsync(walletId, total, WalletTransactionType.TOPUP_REFUND, "Order failed — wallet unlocked",
                orderId, "ORDER", ct);
        }, ct);

        await BestEffort("Notification send", orderId, async () =>
        {
            var info = await db.Orders.AsNoTracking().Where(o => o.Id == orderId)
                .Select(o => new { o.OrderNumber, ProductName = o.GameProduct.Name }).FirstOrDefaultAsync(ct);
            await notifications.SendAsync(new SendNotification(
                userId, "ORDER_FAILED", NotificationChannel.EMAIL, "Order Failed",
                "Your order could not be completed. Balance refunded.",
                new Dictionary<string, object?> { ["orderNumber"] = info?.OrderNumber, ["productName"] = info?.ProductName }), ct);
        });

        await live.NotifyAsync(userId, orderId, "FAILED", ct);
        log.LogError("Order {OrderId} failed: {Reason}", orderId, reason);
    }

    private async Task BestEffort(string what, string orderId, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "{What} failed for order {OrderId}", what, orderId);
        }
    }
}
