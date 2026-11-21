using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.Application.Auth;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

// "Wallets", not "Wallet": a Wallet namespace would hide the Wallet entity in sibling namespaces.
namespace PixelPay.IntegrationTests.Wallets;

public sealed class WalletEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BareUnauthorized = """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""";
    private const string InternalError = """{"success":false,"error":{"message":"Internal server error"}}""";
    private const string Forbidden = """{"success":false,"error":{"statusCode":403,"message":"Forbidden resource","error":"Forbidden"}}""";

    private static readonly string[] WalletKeys = ["id", "userId", "balance", "lockedBalance", "currency", "updatedAt"];

    private static readonly string[] TransactionKeys =
        ["id", "walletId", "type", "amount", "balanceBefore", "balanceAfter", "referenceId", "referenceType", "metadata", "createdAt"];

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string AdminToken => Token(Guid.NewGuid().ToString(), "ADMIN");

    private string Token(string id, string role) =>
        factory.Services.GetRequiredService<ITokenService>().Issue(id, $"{id}@pixelpay.test", role).AccessToken;

    private async Task<(string Id, string Token)> RegisterUser()
    {
        var res = await Send(HttpMethod.Post, "/api/v1/auth/register", JsonSerializer.Serialize(new
        {
            email = $"w{Guid.NewGuid():N}@pixelpay.test", password = "Test1234!", displayName = "Wallet User",
        }));
        var data = await Data(res, HttpStatusCode.Created);
        return (data.GetProperty("user").GetProperty("id").GetString()!, data.GetProperty("accessToken").GetString()!);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string? json = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (json is not null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    private Task<HttpResponseMessage> Deposit(string userId, object amount, string? description = null) =>
        Send(HttpMethod.Post, "/api/v1/wallet/deposit",
            description is null
                ? JsonSerializer.Serialize(new { userId, amount })
                : JsonSerializer.Serialize(new { userId, amount, description }),
            AdminToken);

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

    private static string Error(int status, string message, string error) =>
        $$$"""{"success":false,"error":{"statusCode":{{{status}}},"message":"{{{message}}}","error":"{{{error}}}"}}""";

    private static string Validation(params string[] messages) =>
        $$$"""{"success":false,"error":{"statusCode":400,"message":{{{JsonSerializer.Serialize(messages)}}},"error":"Bad Request"}}""";

    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<string> WalletId(string userId)
    {
        await using var db = factory.Db();
        return (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == userId, Ct)).Id;
    }

    private async Task SeedTransactions(string walletId, int count)
    {
        await using var db = factory.Db();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
            db.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = walletId, Type = WalletTransactionType.DEPOSIT, Amount = i + 1, BalanceBefore = 0, BalanceAfter = i + 1,
                Metadata = new RawJson($$"""{"description":"seed {{i}}"}"""), CreatedAt = start.AddMinutes(i),
            });
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Get_wallet_returns_row_with_empty_transactions()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/wallet", token: u.Token), HttpStatusCode.OK);

        Assert.Equal([.. WalletKeys, "transactions"], Keys(data));
        Assert.Equal(await WalletId(u.Id), Str(data, "id"));
        Assert.Equal(u.Id, Str(data, "userId"));
        Assert.Equal("0", Str(data, "balance"));
        Assert.Equal("0", Str(data, "lockedBalance"));
        Assert.Equal("THB", Str(data, "currency"));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", Str(data, "updatedAt"));
        Assert.Empty(data.GetProperty("transactions").EnumerateArray());
    }

    [Fact]
    public async Task Get_wallet_includes_latest_20_transactions_newest_first()
    {
        var u = await RegisterUser();
        await SeedTransactions(await WalletId(u.Id), 25);

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/wallet", token: u.Token), HttpStatusCode.OK);

        var txs = data.GetProperty("transactions").EnumerateArray().ToArray();
        Assert.Equal(20, txs.Length);
        Assert.Equal(Enumerable.Range(6, 20).Reverse().Select(i => i.ToString()), txs.Select(t => Str(t, "amount")));
        Assert.Equal(TransactionKeys, Keys(txs[0]));
        Assert.Equal("2026-01-01T00:24:00.000Z", Str(txs[0], "createdAt"));
        Assert.Equal(JsonValueKind.Null, txs[0].GetProperty("referenceId").ValueKind);
        Assert.Equal("seed 24", txs[0].GetProperty("metadata").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Get_wallet_without_wallet_returns_404()
    {
        var token = Token(Guid.NewGuid().ToString(), "USER");

        await AssertBody(await Send(HttpMethod.Get, "/api/v1/wallet", token: token), HttpStatusCode.NotFound,
            Error(404, "Wallet not found", "Not Found"));
        await AssertBody(await Send(HttpMethod.Get, "/api/v1/wallet/transactions", token: token), HttpStatusCode.NotFound,
            Error(404, "Wallet not found", "Not Found"));
    }

    [Theory]
    [InlineData("GET", "/api/v1/wallet")]
    [InlineData("GET", "/api/v1/wallet/transactions")]
    [InlineData("POST", "/api/v1/wallet/deposit")]
    public async Task Routes_require_auth(string method, string url) =>
        await AssertBody(await Send(new HttpMethod(method), url, method == "POST" ? "{}" : null), HttpStatusCode.Unauthorized,
            BareUnauthorized);

    [Fact]
    public async Task Transactions_default_page()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/wallet/transactions", token: u.Token), HttpStatusCode.OK);

        Assert.Equal("""{"items":[],"total":0,"page":1,"limit":20}""", data.GetRawText());
    }

    [Fact]
    public async Task Transactions_paginates_newest_first()
    {
        var u = await RegisterUser();
        await SeedTransactions(await WalletId(u.Id), 5);

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/wallet/transactions?page=2&limit=2", token: u.Token),
            HttpStatusCode.OK);

        Assert.Equal(["items", "total", "page", "limit"], Keys(data));
        Assert.Equal(["3", "2"], data.GetProperty("items").EnumerateArray().Select(t => Str(t, "amount")));
        Assert.Equal(TransactionKeys, Keys(data.GetProperty("items")[0]));
        Assert.Equal(5, data.GetProperty("total").GetInt32());
        Assert.Equal(2, data.GetProperty("page").GetInt32());
        Assert.Equal(2, data.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Transactions_parse_query_like_parseInt()
    {
        var u = await RegisterUser();
        await SeedTransactions(await WalletId(u.Id), 3);

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/wallet/transactions?page=2abc&limit=%201.9", token: u.Token),
            HttpStatusCode.OK);

        Assert.Equal(["2"], data.GetProperty("items").EnumerateArray().Select(t => Str(t, "amount")));
        Assert.Equal(2, data.GetProperty("page").GetInt32());
        Assert.Equal(1, data.GetProperty("limit").GetInt32());
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("limit=")]
    public async Task Transactions_unparseable_query_returns_500(string query)
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Get, $"/api/v1/wallet/transactions?{query}", token: u.Token),
            HttpStatusCode.InternalServerError, InternalError);
    }

    [Fact]
    public async Task Deposit_returns_wallet_and_transaction()
    {
        var u = await RegisterUser();
        var walletId = await WalletId(u.Id);

        var data = await Data(await Deposit(u.Id, 100, "Test deposit"), HttpStatusCode.Created);

        Assert.Equal(["wallet", "transaction"], Keys(data));
        var wallet = data.GetProperty("wallet");
        Assert.Equal(WalletKeys, Keys(wallet));
        Assert.Equal(walletId, Str(wallet, "id"));
        Assert.Equal("100", Str(wallet, "balance"));
        var tx = data.GetProperty("transaction");
        Assert.Equal(TransactionKeys, Keys(tx));
        Assert.Equal(walletId, Str(tx, "walletId"));
        Assert.Equal("DEPOSIT", Str(tx, "type"));
        Assert.Equal("100", Str(tx, "amount"));
        Assert.Equal("0", Str(tx, "balanceBefore"));
        Assert.Equal("100", Str(tx, "balanceAfter"));
        Assert.Equal(JsonValueKind.Null, tx.GetProperty("referenceId").ValueKind);
        Assert.Equal(JsonValueKind.Null, tx.GetProperty("referenceType").ValueKind);
        Assert.Equal(["description"], Keys(tx.GetProperty("metadata")));
        Assert.Equal("Test deposit", tx.GetProperty("metadata").GetProperty("description").GetString());

        await using var db = factory.Db();
        var row = await db.WalletTransactions.AsNoTracking().SingleAsync(t => t.WalletId == walletId, Ct);
        Assert.Equal(Str(tx, "id"), row.Id);
        Assert.Equal(Str(tx, "createdAt"), row.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }

    [Fact]
    public async Task Deposit_defaults_description_and_stamps_updated_at()
    {
        var u = await RegisterUser();
        DateTime before;
        await using (var db = factory.Db())
            before = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == u.Id, Ct)).UpdatedAt;
        await Task.Delay(50, Ct);

        var data = await Data(await Deposit(u.Id, 5), HttpStatusCode.Created);

        Assert.Equal("Admin manual deposit", data.GetProperty("transaction").GetProperty("metadata").GetProperty("description").GetString());
        await using var db2 = factory.Db();
        var after = (await db2.Wallets.AsNoTracking().SingleAsync(w => w.UserId == u.Id, Ct)).UpdatedAt;
        Assert.True(after > before, $"{after:O} <= {before:O}");
        Assert.Equal(after.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), Str(data.GetProperty("wallet"), "updatedAt"));
    }

    [Fact]
    public async Task Balance_is_string_without_trailing_zeros()
    {
        var u = await RegisterUser();
        await Data(await Deposit(u.Id, 100), HttpStatusCode.Created);

        var res = await Send(HttpMethod.Get, "/api/v1/wallet", token: u.Token);
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(Ct));

        Assert.Equal("100", json.RootElement.GetProperty("data").GetProperty("balance").GetString());
    }

    [Fact]
    public async Task Deposits_add_decimal_amounts_exactly()
    {
        var u = await RegisterUser();
        await Data(await Send(HttpMethod.Post, "/api/v1/wallet/deposit", $$"""{"userId":"{{u.Id}}","amount":0.1}""", AdminToken),
            HttpStatusCode.Created);

        var data = await Data(await Send(HttpMethod.Post, "/api/v1/wallet/deposit", $$"""{"userId":"{{u.Id}}","amount":0.2}""", AdminToken),
            HttpStatusCode.Created);

        Assert.Equal("0.3", Str(data.GetProperty("wallet"), "balance"));
        Assert.Equal("0.1", Str(data.GetProperty("transaction"), "balanceBefore"));
        Assert.Equal("0.3", Str(data.GetProperty("transaction"), "balanceAfter"));

        data = await Data(await Send(HttpMethod.Post, "/api/v1/wallet/deposit", $$"""{"userId":"{{u.Id}}","amount":2.5e1}""", AdminToken),
            HttpStatusCode.Created);
        Assert.Equal("25.3", Str(data.GetProperty("wallet"), "balance"));
    }

    [Fact]
    public async Task Deposit_shows_in_get_wallet_and_transactions()
    {
        var u = await RegisterUser();
        await Data(await Deposit(u.Id, 100, "Test deposit"), HttpStatusCode.Created);

        var wallet = await Data(await Send(HttpMethod.Get, "/api/v1/wallet", token: u.Token), HttpStatusCode.OK);
        var page = await Data(await Send(HttpMethod.Get, "/api/v1/wallet/transactions", token: u.Token), HttpStatusCode.OK);

        Assert.Equal("100", Str(wallet.GetProperty("transactions")[0], "amount"));
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("DEPOSIT", Str(page.GetProperty("items")[0], "type"));
    }

    [Fact]
    public async Task Deposit_requires_admin()
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Post, "/api/v1/wallet/deposit",
            JsonSerializer.Serialize(new { userId = u.Id, amount = 50 }), u.Token), HttpStatusCode.Forbidden, Forbidden);
    }

    [Fact]
    public async Task Deposit_for_user_without_wallet_returns_404()
    {
        await AssertBody(await Deposit(Guid.NewGuid().ToString(), 5), HttpStatusCode.NotFound,
            Error(404, "Wallet not found for user", "Not Found"));
    }

    [Theory]
    [InlineData("""{"userId":"u","amount":-10}""", "amount must be a positive number")]
    [InlineData("""{"userId":"u","amount":0}""", "amount must be a positive number")]
    [InlineData("""{"userId":"u","amount":"abc"}""",
        "amount must be a positive number|amount must be a number conforming to the specified constraints")]
    [InlineData("""{"userId":"u","amount":"5"}""",
        "amount must be a positive number|amount must be a number conforming to the specified constraints")]
    [InlineData("""{"userId":"u"}""",
        "amount must be a positive number|amount must be a number conforming to the specified constraints")]
    [InlineData("""{"userId":"u","amount":1e400}""", "amount must be a number conforming to the specified constraints")]
    [InlineData("""{"amount":50}""", "userId should not be empty|userId must be a string")]
    [InlineData("""{"userId":"","amount":50}""", "userId should not be empty")]
    [InlineData("""{"userId":5,"amount":50}""", "userId must be a string")]
    [InlineData("""{"userId":"u","amount":50,"description":5}""", "description must be a string")]
    [InlineData("""{"userId":"u","amount":50,"extra":1}""", "property extra should not exist")]
    public async Task Deposit_validates(string json, string messages) =>
        await AssertBody(await Send(HttpMethod.Post, "/api/v1/wallet/deposit", json, AdminToken), HttpStatusCode.BadRequest,
            Validation(messages.Split('|')));

    [Fact]
    public async Task Deposit_null_description_uses_default()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Post, "/api/v1/wallet/deposit",
            $$"""{"userId":"{{u.Id}}","amount":1,"description":null}""", AdminToken), HttpStatusCode.Created);

        Assert.Equal("Admin manual deposit", data.GetProperty("transaction").GetProperty("metadata").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Parallel_deposits_do_not_lose_updates()
    {
        var u = await RegisterUser();
        var walletId = await WalletId(u.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Deposit(u.Id, 5)));

        foreach (var res in responses)
            Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync(Ct));
        await using var db = factory.Db();
        var dbBalance = (await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId, Ct)).Balance;
        var rows = await db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == walletId).ToListAsync(Ct);
        var txRowCount = rows.Count;
        Assert.Equal(100m, dbBalance);
        Assert.Equal(20, txRowCount);
        // Each audit row must reflect the balance it actually moved, so the chain is 0→5→…→100 with no repeats.
        Assert.Equal(Enumerable.Range(0, 20).Select(i => i * 5m), rows.Select(r => r.BalanceBefore).Order());
        Assert.All(rows, r => Assert.Equal(r.BalanceBefore + 5m, r.BalanceAfter));
    }
}
