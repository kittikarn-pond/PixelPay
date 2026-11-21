using System.Net;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Flows;

public sealed class PaymentFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly TestApi _api = new(factory);

    private async Task<(string UserId, string Token, string PaymentId, string Reference)> CreatePayment(decimal amount)
    {
        var (userId, token) = await _api.RegisterUser();
        factory.Jobs.Clear();
        var payment = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/payments/promptpay", new { amount }, token),
            HttpStatusCode.Created);
        return (userId, token, payment.GetProperty("id").GetString()!, payment.GetProperty("gatewayReference").GetString()!);
    }

    [Fact]
    public async Task Create_returns_qr_and_schedules_the_first_poll()
    {
        var (_, token, paymentId, reference) = await CreatePayment(150);

        Assert.Contains($"poll:{paymentId}:1", factory.Jobs.Log);
        var payment = await TestApi.Data(await _api.Send(HttpMethod.Get, $"/api/v1/payments/{paymentId}", token: token), HttpStatusCode.OK);
        Assert.Equal("PENDING", payment.GetProperty("status").GetString());
        Assert.Equal("150", payment.GetProperty("amount").GetString());
        Assert.StartsWith("data:image/png;base64,", payment.GetProperty("qrCodeUrl").GetString());
        Assert.Equal(reference, payment.GetProperty("gatewayReference").GetString());
    }

    [Fact]
    public async Task Amount_outside_10_to_100000_is_rejected()
    {
        var (_, token) = await _api.RegisterUser();
        var res = await _api.Send(HttpMethod.Post, "/api/v1/payments/promptpay", new { amount = 5 }, token);
        Assert.Equal("[\"amount must not be less than 10\"]", await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Webhook_credits_only_after_the_gateway_confirms_and_only_once()
    {
        var (userId, _, paymentId, reference) = await CreatePayment(200);

        // Unconfirmed: the body alone is never trusted.
        await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/payments/webhook", new { referenceNo = reference }), HttpStatusCode.Created);
        Assert.Equal(0m, (await _api.Wallet(userId)).Balance);

        factory.PaymentGateway.Paid[reference] = true;
        await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/payments/webhook", new { referenceNo = reference }), HttpStatusCode.Created);
        await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/payments/webhook", new { referenceNo = reference }), HttpStatusCode.Created);
        await factory.RunJobsAsync(); // the poller must not credit again either

        Assert.Equal(200m, (await _api.Wallet(userId)).Balance);
        Assert.Equal([WalletTransactionType.DEPOSIT], await _api.TransactionTypes(userId));
        await using var db = factory.Db();
        Assert.Equal(PaymentStatus.COMPLETED, db.Payments.Single(p => p.Id == paymentId).Status);
    }

    [Fact]
    public async Task Webhook_rejects_bad_payloads()
    {
        var bad = await _api.Send(HttpMethod.Post, "/api/v1/payments/webhook", "not json");
        Assert.Equal("Invalid webhook payload", await TestApi.ErrorMessage(bad, HttpStatusCode.BadRequest));
        var missing = await _api.Send(HttpMethod.Post, "/api/v1/payments/webhook", new { other = 1 });
        Assert.Equal("Missing referenceNo", await TestApi.ErrorMessage(missing, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Poller_credits_when_paid_and_stops()
    {
        var (userId, _, paymentId, reference) = await CreatePayment(75);
        factory.PaymentGateway.Paid[reference] = true;

        await factory.RunJobsAsync();

        Assert.Equal(75m, (await _api.Wallet(userId)).Balance);
        Assert.DoesNotContain($"poll:{paymentId}:2", factory.Jobs.Log);
    }

    [Fact]
    public async Task Poller_reschedules_while_pending_and_expires_after_the_last_attempt()
    {
        var (userId, _, paymentId, _) = await CreatePayment(30);

        await factory.RunJobsAsync(max: 500);

        Assert.Contains($"poll:{paymentId}:90", factory.Jobs.Log);
        Assert.DoesNotContain($"poll:{paymentId}:91", factory.Jobs.Log);
        await using var db = factory.Db();
        Assert.Equal(PaymentStatus.EXPIRED, db.Payments.Single(p => p.Id == paymentId).Status);
        Assert.Equal(0m, (await _api.Wallet(userId)).Balance);
    }

    [Fact]
    public async Task Gateway_failure_marks_the_payment_failed()
    {
        var (_, token) = await _api.RegisterUser();
        factory.PaymentGateway.FailCreate = true;
        try
        {
            var res = await _api.Send(HttpMethod.Post, "/api/v1/payments/promptpay", new { amount = 50 }, token);
            Assert.Equal("Failed to create payment. Please try again.", await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
        }
        finally
        {
            factory.PaymentGateway.FailCreate = false;
        }
    }
}

public sealed class CatalogAdminFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Admin_game_product_category_crud_reaches_the_storefront()
    {
        var admin = _api.AdminToken;
        var slug = $"g-{Guid.NewGuid():N}"[..20];
        var category = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/admin/categories",
            new { name = "Cat", slug = $"c-{Guid.NewGuid():N}"[..20] }, admin), HttpStatusCode.Created);
        var game = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/admin/games",
            new { name = "New Game", slug, categoryId = category.GetProperty("id").GetString(), sortOrder = 3 }, admin),
            HttpStatusCode.Created);
        var gameId = game.GetProperty("id").GetString()!;
        Assert.Equal("{}", game.GetProperty("metadata").GetRawText());

        var product = await TestApi.Data(await _api.Send(HttpMethod.Post, $"/api/v1/admin/games/{gameId}/products",
            new { name = "60 UC", sku = $"S-{Guid.NewGuid():N}"[..16], priceCost = 27.9, priceSell = 30 }, admin),
            HttpStatusCode.Created);
        Assert.Equal("30", product.GetProperty("priceSell").GetString());
        Assert.Equal("DIRECT", product.GetProperty("productType").GetString());

        var publicGame = await TestApi.Data(await _api.Send(HttpMethod.Get, $"/api/v1/games/{slug}"), HttpStatusCode.OK);
        Assert.Equal("Cat", publicGame.GetProperty("category").GetProperty("name").GetString());
        var products = await TestApi.Data(await _api.Send(HttpMethod.Get, $"/api/v1/games/{slug}/products"), HttpStatusCode.OK);
        Assert.Equal(1, products.GetArrayLength());

        var dup = await _api.Send(HttpMethod.Post, "/api/v1/admin/games", new { name = "Dup", slug }, admin);
        Assert.Equal($"Slug \"{slug}\" is already in use", await TestApi.ErrorMessage(dup, HttpStatusCode.Conflict));

        // Clearing the category with an explicit null, then deleting the game with its products.
        var cleared = await TestApi.Data(await _api.Send(HttpMethod.Patch, $"/api/v1/admin/games/{gameId}",
            """{"categoryId":null}""", admin), HttpStatusCode.OK);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, cleared.GetProperty("categoryId").ValueKind);
        var deleted = await _api.Send(HttpMethod.Delete, $"/api/v1/admin/games/{gameId}", token: admin);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Send(HttpMethod.Get, $"/api/v1/games/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Admin_validation_messages_follow_class_validator()
    {
        var res = await _api.Send(HttpMethod.Post, "/api/v1/admin/games", new { name = 5, slug = "Bad Slug", logoUrl = "nope" },
            _api.AdminToken);
        Assert.Equal("""["name must be shorter than or equal to 100 characters","name must be a string","slug must be lowercase letters, numbers, and hyphens only","logoUrl must be a URL address"]""",
            await TestApi.ErrorMessage(res, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Provider_credentials_are_encrypted_and_never_returned()
    {
        var created = await TestApi.Data(await _api.Send(HttpMethod.Post, "/api/v1/admin/providers",
            new { name = "UniPin", slug = "unipin", apiUrl = "http://unipin.local:8080", apiKey = "plain-key", healthCheckUrl = "" },
            _api.AdminToken), HttpStatusCode.Created);

        Assert.False(created.TryGetProperty("apiKeyEnc", out _));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, created.GetProperty("healthCheckUrl").ValueKind);
        await using var db = factory.Db();
        var stored = db.Providers.Single(p => p.Id == created.GetProperty("id").GetString());
        Assert.Matches("^[0-9a-f]{24}:[0-9a-f]{32}:[0-9a-f]+$", stored.ApiKeyEnc);
        Assert.DoesNotContain("plain-key", stored.ApiKeyEnc);
    }

    [Fact]
    public async Task Non_admin_gets_403_on_admin_routes()
    {
        var (_, token) = await _api.RegisterUser();
        var res = await _api.Send(HttpMethod.Get, "/api/v1/admin/stats", token: token);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_stats_and_analytics_answer()
    {
        var stats = await TestApi.Data(await _api.Send(HttpMethod.Get, "/api/v1/admin/stats", token: _api.AdminToken), HttpStatusCode.OK);
        Assert.True(stats.GetProperty("totalUsers").GetInt32() >= 0);
        foreach (var path in new[] { "analytics/revenue?period=day", "analytics/revenue", "analytics/orders?period=week", "analytics/users" })
            await TestApi.Data(await _api.Send(HttpMethod.Get, $"/api/v1/admin/{path}", token: _api.AdminToken), HttpStatusCode.OK);
    }
}
