using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.Application.Auth;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Orders;

public sealed class OrdersReadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BareUnauthorized = """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""";
    private const string InternalError = """{"success":false,"error":{"message":"Internal server error"}}""";

    private static readonly string[] OrderKeys =
    [
        "id", "orderNumber", "userId", "gameProductId", "providerId", "couponId", "quantity", "unitPrice", "totalPrice",
        "discountAmount", "cashbackAmount", "paymentMethod", "gameUid", "gameServer", "gameUsername", "status",
        "providerOrderId", "providerReference", "retryCount", "metadata", "createdAt", "updatedAt", "completedAt",
    ];

    private static readonly string[] ProductKeys =
    [
        "id", "gameId", "name", "sku", "priceCost", "priceSell", "currency", "productType", "requiresServer",
        "requiresUsername", "isActive", "sortOrder", "metadata", "createdAt", "updatedAt",
    ];

    private static readonly string[] GameKeys =
    [
        "id", "name", "slug", "logoUrl", "bannerUrl", "description", "descriptionTh", "categoryId", "isActive", "sortOrder",
        "metadata", "createdAt", "updatedAt",
    ];

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Token(string userId) =>
        factory.Services.GetRequiredService<ITokenService>().Issue(userId, $"{userId}@pixelpay.test", "USER").AccessToken;

    private async Task<string> NewUser()
    {
        await using var db = factory.Db();
        var user = new User { Email = $"o{Guid.NewGuid():N}@pixelpay.test", PasswordHash = "x", DisplayName = "Buyer", ReferralCode = Guid.NewGuid().ToString("N")[..10] };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<GameProduct> NewProduct()
    {
        await using var db = factory.Db();
        var game = new Game
        {
            Name = "Game", Slug = $"g{Guid.NewGuid():N}", Description = "desc", DescriptionTh = "คำอธิบาย",
            Metadata = new RawJson("""{"genre":"moba"}"""),
        };
        var product = new GameProduct
        {
            Game = game, Name = "60 Diamonds", Sku = $"sku{Guid.NewGuid():N}", PriceCost = 40.1234m, PriceSell = 49.5m, Currency = "THB",
        };
        db.GameProducts.Add(product);
        await db.SaveChangesAsync(Ct);
        return product;
    }

    private async Task<Order> NewOrder(string userId, string productId, DateTime? createdAt = null)
    {
        await using var db = factory.Db();
        var order = new Order
        {
            OrderNumber = $"PP{Guid.NewGuid():N}"[..20], UserId = userId, GameProductId = productId, Quantity = 1,
            UnitPrice = 49.5m, TotalPrice = 49.5m, PaymentMethod = PaymentMethod.WALLET, GameUid = "12345678",
            Status = OrderStatus.PENDING, Metadata = new RawJson("""{"note":"hi","n":[1,2]}"""),
        };
        if (createdAt is { } c)
            order.CreatedAt = c;
        db.Orders.Add(order);
        await db.SaveChangesAsync(Ct);
        return order;
    }

    private Task<HttpResponseMessage> Get(string url, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage res, HttpStatusCode status)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.StatusCode == status, $"{res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static async Task AssertBody(HttpResponseMessage res, HttpStatusCode status, string body)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(body, await res.Content.ReadAsStringAsync(Ct));
    }

    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public async Task List_is_empty_for_new_user()
    {
        var data = await Data(await Get("/api/v1/orders", Token(await NewUser())), HttpStatusCode.OK);

        Assert.Equal("""{"items":[],"total":0,"page":1,"limit":20}""", data.GetRawText());
    }

    [Fact]
    public async Task List_returns_own_orders_newest_first_with_full_rows()
    {
        var me = await NewUser();
        var other = await NewUser();
        var product = await NewProduct();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var old = await NewOrder(me, product.Id, start);
        var recent = await NewOrder(me, product.Id, start.AddHours(1));
        await NewOrder(other, product.Id, start.AddHours(2));

        var data = await Data(await Get("/api/v1/orders", Token(me)), HttpStatusCode.OK);

        Assert.Equal(["items", "total", "page", "limit"], Keys(data));
        Assert.Equal(2, data.GetProperty("total").GetInt32());
        var items = data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([recent.Id, old.Id], items.Select(i => Str(i, "id")));
        Assert.Equal(OrderKeys, Keys(items[0]));
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("couponId").ValueKind);
        Assert.Equal("2026-01-01T01:00:00.000Z", Str(items[0], "createdAt"));
    }

    [Fact]
    public async Task List_paginates()
    {
        var me = await NewUser();
        var product = await NewProduct();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var orders = new List<Order>();
        for (var i = 0; i < 5; i++)
            orders.Add(await NewOrder(me, product.Id, start.AddMinutes(i)));

        var data = await Data(await Get("/api/v1/orders?page=2&limit=2", Token(me)), HttpStatusCode.OK);

        Assert.Equal([orders[2].Id, orders[1].Id], data.GetProperty("items").EnumerateArray().Select(i => Str(i, "id")));
        Assert.Equal(5, data.GetProperty("total").GetInt32());
        Assert.Equal(2, data.GetProperty("page").GetInt32());
        Assert.Equal(2, data.GetProperty("limit").GetInt32());
    }

    [Theory]
    [InlineData("page=2.5&limit=20", 0, 2.5, 20)]
    [InlineData("limit=1e1", 10, 1, 10)]
    [InlineData("limit=", 0, 1, 0)]
    public async Task List_parses_query_like_unary_plus(string query, int expectedItems, double page, double limit)
    {
        var me = await NewUser();
        var product = await NewProduct();
        for (var i = 0; i < 12; i++)
            await NewOrder(me, product.Id);

        var data = await Data(await Get($"/api/v1/orders?{query}", Token(me)), HttpStatusCode.OK);

        Assert.Equal(expectedItems, data.GetProperty("items").GetArrayLength());
        Assert.Equal(12, data.GetProperty("total").GetInt32());
        Assert.Equal(page, data.GetProperty("page").GetDouble());
        Assert.Equal(limit, data.GetProperty("limit").GetDouble());
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("page=12abc")]
    [InlineData("limit=5.5")]
    [InlineData("page=1&page=2")]
    [InlineData("page=0")]
    public async Task List_invalid_paging_returns_500(string query) =>
        await AssertBody(await Get($"/api/v1/orders?{query}", Token(await NewUser())), HttpStatusCode.InternalServerError, InternalError);

    [Fact]
    public async Task Order_json_has_string_money_and_raw_metadata()
    {
        var me = await NewUser();
        var order = await NewOrder(me, (await NewProduct()).Id);

        var data = await Data(await Get($"/api/v1/orders/{order.Id}", Token(me)), HttpStatusCode.OK);

        Assert.Equal("49.5", Str(data, "totalPrice"));
        Assert.Equal("49.5", Str(data, "unitPrice"));
        Assert.Equal("0", Str(data, "discountAmount"));
        Assert.Equal(JsonValueKind.Object, data.GetProperty("metadata").ValueKind);
        // MySQL re-serializes JSON columns (key order, spacing), so compare values.
        Assert.Equal("hi", data.GetProperty("metadata").GetProperty("note").GetString());
        Assert.Equal([1, 2], data.GetProperty("metadata").GetProperty("n").EnumerateArray().Select(e => e.GetInt32()));
    }

    [Fact]
    public async Task Get_by_id_includes_product_and_game()
    {
        var me = await NewUser();
        var product = await NewProduct();
        var order = await NewOrder(me, product.Id);

        var data = await Data(await Get($"/api/v1/orders/{order.Id}", Token(me)), HttpStatusCode.OK);

        Assert.Equal([.. OrderKeys, "gameProduct"], Keys(data));
        Assert.Equal(order.Id, Str(data, "id"));
        Assert.Equal("PENDING", Str(data, "status"));
        Assert.Equal("WALLET", Str(data, "paymentMethod"));
        var p = data.GetProperty("gameProduct");
        Assert.Equal([.. ProductKeys, "game"], Keys(p));
        Assert.Equal(product.Id, Str(p, "id"));
        Assert.Equal("40.1234", Str(p, "priceCost"));
        Assert.Equal("49.5", Str(p, "priceSell"));
        Assert.Equal("DIRECT", Str(p, "productType"));
        var g = p.GetProperty("game");
        Assert.Equal(GameKeys, Keys(g));
        Assert.Equal(product.GameId, Str(g, "id"));
        Assert.Equal("คำอธิบาย", Str(g, "descriptionTh"));
        Assert.Equal(JsonValueKind.Null, g.GetProperty("logoUrl").ValueKind);
        Assert.Equal("moba", g.GetProperty("metadata").GetProperty("genre").GetString());
    }

    [Fact]
    public async Task Get_missing_order_returns_404() =>
        await AssertBody(await Get($"/api/v1/orders/{Guid.NewGuid()}", Token(await NewUser())), HttpStatusCode.NotFound,
            """{"success":false,"error":{"statusCode":404,"message":"Order not found","error":"Not Found"}}""");

    [Fact]
    public async Task Get_foreign_order_returns_bare_401()
    {
        var order = await NewOrder(await NewUser(), (await NewProduct()).Id);

        await AssertBody(await Get($"/api/v1/orders/{order.Id}", Token(await NewUser())), HttpStatusCode.Unauthorized, BareUnauthorized);
    }

    [Theory]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/orders/abc")]
    public async Task Routes_require_auth(string url) =>
        await AssertBody(await Get(url, null), HttpStatusCode.Unauthorized, BareUnauthorized);
}
