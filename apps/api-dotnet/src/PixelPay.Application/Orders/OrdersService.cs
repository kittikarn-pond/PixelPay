using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PixelPay.Application.Common;
using PixelPay.Application.Promotions;
using PixelPay.Application.Wallets;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Orders;

public sealed record CreateOrderDto(
    JsonElement GameProductId, JsonElement PaymentMethod, JsonElement GameUid, JsonElement GameServer,
    JsonElement GameUsername, JsonElement CouponCode);

public sealed class CreateOrderDtoValidator : DtoValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        Field(x => x.GameProductId, "gameProductId").Uuid();
        Field(x => x.PaymentMethod, "paymentMethod").Enum<PaymentMethod>();
        Field(x => x.GameUid, "gameUid").NotEmpty().String().MaxLength(100);
        Field(x => x.GameServer, "gameServer").Optional().String().MaxLength(100);
        Field(x => x.GameUsername, "gameUsername").Optional().String().MaxLength(100);
        Field(x => x.CouponCode, "couponCode").Optional().String().MaxLength(50);
    }
}

public sealed class OrdersService(
    IAppDbContext db, WalletService wallet, CouponsService coupons, IJobQueue jobs, TimeProvider clock,
    ILogger<OrdersService> log)
{
    public async Task<Dictionary<string, object?>> CreateAsync(string userId, CreateOrderDto dto, CancellationToken ct)
    {
        var productId = dto.GameProductId.Str()!;
        var product = await db.GameProducts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct)
            ?? throw new BadRequestException("Product not found or unavailable");

        // Validation does not reserve the coupon; ApplyToOrderAsync re-checks the usage limit atomically.
        decimal discount = 0;
        string? couponId = null;
        if (dto.CouponCode.Str() is { Length: > 0 } code)
        {
            var result = await coupons.ValidateAsync(userId, code, product.PriceSell, product.GameId, ct);
            discount = (decimal)result.DiscountAmount;
            couponId = result.CouponId;
        }

        var totalPrice = product.PriceSell - discount;
        var order = await Tx.RunAsync(db, async () =>
        {
            var walletId = await wallet.WalletIdForUserAsync(userId, ct);
            var newOrder = new Order
            {
                OrderNumber = NewOrderNumber(),
                UserId = userId,
                GameProductId = productId,
                Quantity = 1,
                UnitPrice = product.PriceSell,
                TotalPrice = totalPrice,
                DiscountAmount = discount,
                CashbackAmount = 0,
                PaymentMethod = Enum.Parse<PaymentMethod>(dto.PaymentMethod.Str()!),
                GameUid = dto.GameUid.Str()!,
                GameServer = dto.GameServer.Str(),
                GameUsername = dto.GameUsername.Str(),
                Status = OrderStatus.PENDING,
            };
            db.Orders.Add(newOrder);
            await db.SaveChangesAsync(ct);

            // Same transaction as the order: no order without its wallet lock, and the reverse.
            await wallet.LockAsync(walletId, totalPrice, "Wallet lock for order", newOrder.Id, "ORDER", ct);
            return newOrder;
        }, ct);

        if (couponId is not null)
        {
            try
            {
                await coupons.ApplyToOrderAsync(couponId, userId, order.Id, discount, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The order stands; the coupon usage simply was not recorded (same as Node).
                log.LogWarning(ex, "Failed to apply coupon {CouponId} to order {OrderId}", couponId, order.Id);
            }
        }

        // Enqueue only after commit: the worker must find the order and its lock.
        jobs.EnqueueTopup(order.Id, userId, productId);

        var stored = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id, ct);
        return Rows.Of(stored);
    }

    public async Task<object> CancelAsync(string id, string userId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order not found");
        if (order.UserId != userId)
            throw new UnauthorizedException();

        await Tx.RunAsync(db, async () =>
        {
            // Conditional on PENDING so the worker and a cancel can never both act on the order.
            // A local, not a call inside the lambda: EF would inline that as a seconds-only literal.
            var now = Domain.Common.PrismaTime.Now();
            var updated = await db.Orders.Where(o => o.Id == id && o.Status == OrderStatus.PENDING)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.CANCELLED)
                    .SetProperty(o => o.UpdatedAt, now), ct);
            if (updated == 0)
                throw new BadRequestException("Only pending orders can be cancelled");

            var walletId = await wallet.WalletIdForUserAsync(order.UserId, ct);
            await wallet.UnlockAsync(walletId, order.TotalPrice, WalletTransactionType.TOPUP_REFUND,
                "Order cancelled — wallet unlocked", order.Id, "ORDER", ct);
        }, ct);
        return new { message = "Order cancelled" };
    }

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>PP-YYYYMMDD-XXXXXXXX (server local date, 8 random chars), as Node generated it.</summary>
    private string NewOrderNumber() =>
        $"PP-{clock.GetLocalNow():yyyyMMdd}-{RandomNumberGenerator.GetString(Alphabet, 8)}";
}
