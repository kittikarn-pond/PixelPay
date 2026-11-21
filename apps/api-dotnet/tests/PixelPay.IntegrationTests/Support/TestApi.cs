using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.Application.Auth;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.IntegrationTests.Support;

/// <summary>HTTP and data helpers shared by the flow tests.</summary>
public sealed class TestApi(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public string AdminToken => Token(Guid.NewGuid().ToString(), "ADMIN");

    public string Token(string id, string role) =>
        factory.Services.GetRequiredService<ITokenService>().Issue(id, $"{id}@pixelpay.test", role).AccessToken;

    /// <summary>An admin that exists in the users table (the audit log has a foreign key to it).</summary>
    public async Task<string> RealAdminToken()
    {
        var (id, _) = await RegisterUser();
        await using var db = factory.Db();
        var user = db.Users.Single(u => u.Id == id);
        user.Role = UserRole.ADMIN;
        await db.SaveChangesAsync(Ct);
        return factory.Services.GetRequiredService<ITokenService>().Issue(id, user.Email, "ADMIN").AccessToken;
    }

    public async Task<(string Id, string Token)> RegisterUser(string? referralCode = null)
    {
        var res = await Send(HttpMethod.Post, "/api/v1/auth/register", new
        {
            email = $"f{Guid.NewGuid():N}@pixelpay.test",
            password = "Test1234!",
            displayName = "Flow User",
            referredBy = referralCode,
        });
        var data = await Data(res, HttpStatusCode.Created);
        return (data.GetProperty("user").GetProperty("id").GetString()!, data.GetProperty("accessToken").GetString()!);
    }

    public async Task Fund(string userId, decimal amount) =>
        await Data(await Send(HttpMethod.Post, "/api/v1/wallet/deposit", new { userId, amount }, AdminToken), HttpStatusCode.Created);

    public Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(body as string ?? JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    public static async Task<JsonElement> Data(HttpResponseMessage res, HttpStatusCode status)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.StatusCode == status, $"{res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean(), body);
        return doc.RootElement.GetProperty("data").Clone();
    }

    public static async Task<string> ErrorMessage(HttpResponseMessage res, HttpStatusCode status)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.StatusCode == status, $"{res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("error").GetProperty("message").ToString();
    }

    /// <summary>An active game with one product priced <paramref name="price"/> and one active provider mapping.</summary>
    public async Task<(string GameId, string ProductId)> CreateProduct(decimal price, int providers = 1)
    {
        await using var db = factory.Db();
        var game = new Game { Name = "Flow Game", Slug = $"flow-{Guid.NewGuid():N}"[..30] };
        var product = new GameProduct
        {
            Game = game, Name = "100 Gems", Sku = $"SKU-{Guid.NewGuid():N}"[..20], PriceCost = price * 0.9m, PriceSell = price,
        };
        db.Games.Add(game);
        db.GameProducts.Add(product);
        for (var i = 0; i < providers; i++)
        {
            var provider = new Provider
            {
                Name = $"P{i}", Slug = $"fake{i}-{Guid.NewGuid():N}"[..20], ApiUrl = "http://provider.test",
                ApiKeyEnc = "unused", Priority = i + 1,
            };
            db.Providers.Add(provider);
            db.ProviderProducts.Add(new ProviderProduct { Provider = provider, GameProduct = product, ProviderSku = $"psku{i}" });
        }
        await db.SaveChangesAsync(Ct);
        return (game.Id, product.Id);
    }

    public async Task<Wallet> Wallet(string userId)
    {
        await using var db = factory.Db();
        return db.Wallets.Single(w => w.UserId == userId);
    }

    public async Task<List<WalletTransactionType>> TransactionTypes(string userId)
    {
        await using var db = factory.Db();
        var walletId = db.Wallets.Single(w => w.UserId == userId).Id;
        return db.WalletTransactions.Where(t => t.WalletId == walletId).OrderBy(t => t.CreatedAt).Select(t => t.Type).ToList();
    }

    public async Task<Order> Order(string id)
    {
        await using var db = factory.Db();
        return db.Orders.Single(o => o.Id == id);
    }
}
