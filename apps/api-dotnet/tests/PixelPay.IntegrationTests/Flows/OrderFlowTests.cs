using System.Net;
using PixelPay.Application.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Flows;

public sealed class OrderFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly TestApi _api = new(factory);

    private static CancellationToken Ct => TestApi.Ct;

    private async Task<(string UserId, string Token, string OrderId)> PlaceOrder(decimal price, decimal funds, int providers = 1,
        string? coupon = null)
    {
        var (userId, token) = await _api.RegisterUser();
        await _api.Fund(userId, funds);
        var (_, productId) = await _api.CreateProduct(price, providers);
        factory.Jobs.Clear();
        var order = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/orders",
            new { gameProductId = productId, paymentMethod = "WALLET", gameUid = "123456", couponCode = coupon }, token),
            HttpStatusCode.Created);
        return (userId, token, order.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Create_locks_funds_and_queues_topup()
    {
        var (userId, _, orderId) = await PlaceOrder(price: 49.50m, funds: 100);

        var wallet = await _api.Wallet(userId);
        Assert.Equal(50.50m, wallet.Balance);
        Assert.Equal(49.50m, wallet.LockedBalance);
        Assert.Equal(OrderStatus.PENDING, (await _api.Order(orderId)).Status);
        Assert.Contains($"topup:{orderId}", factory.Jobs.Log);
        Assert.Matches(@"^PP-\d{8}-[A-Z0-9]{8}$", (await _api.Order(orderId)).OrderNumber);
    }

    [Fact]
    public async Task Successful_topup_completes_order_and_spends_the_lock()
    {
        factory.Providers.Next = _ => new TopupResult(true, "prov-77", "serial-77");
        var (userId, _, orderId) = await PlaceOrder(price: 30, funds: 100);

        await factory.RunJobsAsync();

        var order = await _api.Order(orderId);
        Assert.Equal(OrderStatus.COMPLETED, order.Status);
        Assert.Equal("prov-77", order.ProviderOrderId);
        Assert.NotNull(order.CompletedAt);
        var wallet = await _api.Wallet(userId);
        Assert.Equal(70m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
        Assert.Equal([WalletTransactionType.DEPOSIT, WalletTransactionType.LOCK, WalletTransactionType.TOPUP_DEBIT],
            await _api.TransactionTypes(userId));
        Assert.Contains((userId, orderId, "COMPLETED"), factory.OrderNotifier.Sent);
        await using var db = factory.Db();
        Assert.Single(db.Notifications.Where(n => n.UserId == userId && n.Type == "ORDER_COMPLETED"));
    }

    [Fact]
    public async Task Topup_tries_providers_in_priority_order_then_refunds_when_all_fail()
    {
        factory.Providers.Next = r => new TopupResult(false, FailureReason: $"down:{r.ProviderSku}");
        var (userId, _, orderId) = await PlaceOrder(price: 25, funds: 25, providers: 2);
        factory.Providers.Calls.Clear();

        await factory.RunJobsAsync();

        Assert.Equal(["psku0", "psku1"], factory.Providers.Calls.Select(c => c.ProviderSku));
        var order = await _api.Order(orderId);
        Assert.Equal(OrderStatus.FAILED, order.Status);
        Assert.Equal(2, order.RetryCount);
        Assert.Contains("down:psku1", order.Metadata!.Value.Value);
        var wallet = await _api.Wallet(userId);
        Assert.Equal(25m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
        Assert.Contains((userId, orderId, "FAILED"), factory.OrderNotifier.Sent);
    }

    [Fact]
    public async Task Provider_exception_falls_through_to_the_next_provider()
    {
        var calls = 0;
        factory.Providers.Next = _ => ++calls == 1 ? throw new HttpRequestException("timeout") : new TopupResult(true, "ok");
        var (_, _, orderId) = await PlaceOrder(price: 10, funds: 10, providers: 2);

        await factory.RunJobsAsync();

        var order = await _api.Order(orderId);
        Assert.Equal(OrderStatus.COMPLETED, order.Status);
        Assert.Equal(1, order.RetryCount);
    }

    [Fact]
    public async Task Insufficient_balance_rejects_and_leaves_no_order()
    {
        var (userId, token) = await _api.RegisterUser();
        await _api.Fund(userId, 5);
        var (_, productId) = await _api.CreateProduct(10);

        var res = await _api.Send(HttpMethod.Post, "/api/v1/orders",
            new { gameProductId = productId, paymentMethod = "WALLET", gameUid = "1" }, token);

        Assert.Equal("Insufficient balance to lock", await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
        await using var db = factory.Db();
        Assert.Empty(db.Orders.Where(o => o.UserId == userId));
        Assert.Equal(5m, (await _api.Wallet(userId)).Balance);
    }

    [Fact]
    public async Task Cancel_unlocks_once_and_the_worker_then_skips_the_order()
    {
        var (userId, token, orderId) = await PlaceOrder(price: 20, funds: 20);

        await TestApi.Data(await _api.Send(HttpMethod.Post, $"/api/v1/orders/{orderId}/cancel", token: token), HttpStatusCode.Created);
        var again = await _api.Send(HttpMethod.Post, $"/api/v1/orders/{orderId}/cancel", token: token);
        Assert.Equal("Only pending orders can be cancelled", await TestApi.ErrorMessage(again, HttpStatusCode.BadRequest));

        factory.Providers.Calls.Clear();
        await factory.RunJobsAsync();
        Assert.Empty(factory.Providers.Calls);
        Assert.Equal(OrderStatus.CANCELLED, (await _api.Order(orderId)).Status);
        var wallet = await _api.Wallet(userId);
        Assert.Equal(20m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
    }

    [Fact]
    public async Task Cancel_someone_elses_order_is_401()
    {
        var (_, _, orderId) = await PlaceOrder(price: 5, funds: 5);
        var (_, other) = await _api.RegisterUser();
        var res = await _api.Send(HttpMethod.Post, $"/api/v1/orders/{orderId}/cancel", token: other);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Coupon_discount_reduces_the_lock_and_records_usage()
    {
        var code = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await using (var db = factory.Db())
        {
            db.Coupons.Add(new Coupon { Code = code, DiscountType = DiscountType.PERCENTAGE, Value = 10, UsageLimit = 1 });
            await db.SaveChangesAsync(Ct);
        }

        var (userId, _, orderId) = await PlaceOrder(price: 33.33m, funds: 100, coupon: code);

        var order = await _api.Order(orderId);
        Assert.Equal(3.33m, order.DiscountAmount);
        Assert.Equal(30.00m, order.TotalPrice);
        Assert.Equal(30.00m, (await _api.Wallet(userId)).LockedBalance);
        await using var check = factory.Db();
        Assert.Equal(1, check.Coupons.Single(c => c.Code == code).UsedCount);
        Assert.Single(check.CouponUsages.Where(u => u.OrderId == orderId));

        // Usage limit 1 is now reached for everyone.
        var (other, otherToken) = await _api.RegisterUser();
        await _api.Fund(other, 100);
        var res = await _api.Send(HttpMethod.Post, "/api/v1/coupons/validate", new { code, orderTotal = 50 }, otherToken);
        Assert.Equal("Coupon usage limit reached", await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Completed_order_pays_cashback_and_referral_commission()
    {
        var (referrerId, _) = await _api.RegisterUser();
        string referralCode;
        await using (var db = factory.Db())
        {
            referralCode = db.Users.Single(u => u.Id == referrerId).ReferralCode;
            db.CashbackRules.Add(new CashbackRule { Name = "5%", CashbackType = CashbackType.PERCENTAGE, Value = 5 });
            await db.SaveChangesAsync(Ct);
        }
        var (buyerId, buyerToken) = await _api.RegisterUser(referralCode);
        await _api.Fund(buyerId, 200);
        var (_, productId) = await _api.CreateProduct(100);
        factory.Providers.Next = _ => new TopupResult(true, "x");
        factory.Jobs.Clear();
        var order = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/orders",
            new { gameProductId = productId, paymentMethod = "WALLET", gameUid = "9" }, buyerToken), HttpStatusCode.Created);

        await factory.RunJobsAsync();

        var orderId = order.GetProperty("id").GetString()!;
        Assert.True((await _api.Order(orderId)).CashbackAmount >= 5m);
        Assert.Contains(WalletTransactionType.CASHBACK_CREDIT, await _api.TransactionTypes(buyerId));
        await using var check = factory.Db();
        var commission = check.AffiliateCommissions.Single(c => c.OrderId == orderId);
        Assert.Equal(2.00m, commission.CommissionAmount);
    }

    [Fact]
    public async Task Admin_refund_of_a_completed_order_credits_the_buyer_back()
    {
        factory.Providers.Next = _ => new TopupResult(true, "x");
        var (userId, _, orderId) = await PlaceOrder(price: 40, funds: 40);
        await factory.RunJobsAsync();
        Assert.Equal(0m, (await _api.Wallet(userId)).Balance);

        var data = await TestApi.Data(await _api.Send(HttpMethod.Patch, $"/api/v1/admin/orders/{orderId}/status",
            new { status = "REFUNDED" }, await _api.RealAdminToken()), HttpStatusCode.OK);

        Assert.Equal("REFUNDED", data.GetProperty("status").GetString());
        Assert.Equal(40m, (await _api.Wallet(userId)).Balance);
        await using var db = factory.Db();
        var actions = db.AuditLogs.Select(a => a.Action).ToList();
        Assert.True(actions.Contains("PATCH:AdminController:overrideOrderStatus"), string.Join(", ", actions));
    }

    [Fact]
    public async Task Admin_fail_override_of_a_pending_order_releases_the_lock()
    {
        var (userId, _, orderId) = await PlaceOrder(price: 15, funds: 15);

        await TestApi.Data(await _api.Send(HttpMethod.Patch, $"/api/v1/admin/orders/{orderId}/status",
            new { status = "FAILED" }, _api.AdminToken), HttpStatusCode.OK);

        var wallet = await _api.Wallet(userId);
        Assert.Equal(15m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
        var bad = await _api.Send(HttpMethod.Patch, $"/api/v1/admin/orders/{orderId}/status", new { status = "PENDING" }, _api.AdminToken);
        Assert.Equal("Invalid status", await TestApi.ErrorMessage(bad, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Admin_retry_relocks_funds_and_a_successful_retry_debits_them()
    {
        factory.Providers.Next = _ => new TopupResult(false, FailureReason: "no");
        var (userId, _, orderId) = await PlaceOrder(price: 12, funds: 24);
        await factory.RunJobsAsync();
        Assert.Equal(OrderStatus.FAILED, (await _api.Order(orderId)).Status);
        Assert.Equal(24m, (await _api.Wallet(userId)).Balance);

        var res = await _api.Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/retry", token: _api.AdminToken);
        Assert.Equal("Order queued for retry", (await TestApi.Data(res, HttpStatusCode.Created)).GetProperty("message").GetString());
        Assert.Equal(OrderStatus.PENDING, (await _api.Order(orderId)).Status);
        Assert.Equal(12m, (await _api.Wallet(userId)).LockedBalance);

        factory.Providers.Next = _ => new TopupResult(true, "retry-ok");
        await factory.RunJobsAsync();

        Assert.Equal(OrderStatus.COMPLETED, (await _api.Order(orderId)).Status);
        var wallet = await _api.Wallet(userId);
        Assert.Equal(12m + (await _api.Order(orderId)).CashbackAmount, wallet.Balance); // cashback rules from other tests may apply
        Assert.Equal(0m, wallet.LockedBalance);
    }

    [Fact]
    public async Task Admin_retry_without_funds_is_rejected_and_the_order_stays_failed()
    {
        factory.Providers.Next = _ => new TopupResult(false, FailureReason: "no");
        var (userId, token, orderId) = await PlaceOrder(price: 12, funds: 12);
        await factory.RunJobsAsync();
        // The user spends the refunded balance elsewhere.
        var (_, otherProduct) = await _api.CreateProduct(12);
        await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/orders",
            new { gameProductId = otherProduct, paymentMethod = "WALLET", gameUid = "1" }, token), HttpStatusCode.Created);

        var res = await _api.Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/retry", token: _api.AdminToken);

        Assert.Equal("Insufficient balance to lock", await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
        Assert.Equal(OrderStatus.FAILED, (await _api.Order(orderId)).Status);
        Assert.Equal(12m, (await _api.Wallet(userId)).LockedBalance);
    }
}
