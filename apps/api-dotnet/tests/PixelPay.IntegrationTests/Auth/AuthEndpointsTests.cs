using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Auth;

public sealed class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Generated once with: node -e "require('bcrypt').hash('Test1234!',12).then(console.log)"
    private const string NodeBcryptHash = "$2b$12$4wmjOw0U6aKLU41TZmOsGObLgqooGarclnOHiPTDR1ewJO3Ul5MjC";
    private const string Password = "Test1234!";
    private const string BareUnauthorized = """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""";
    private const string InvalidCredentials =
        """{"success":false,"error":{"statusCode":401,"message":"Invalid credentials","error":"Unauthorized"}}""";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"u{Guid.NewGuid():N}@pixelpay.test";

    private Task<HttpResponseMessage> Post(string url, string json, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string? token)
    {
        var req = new HttpRequestMessage(method, url);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    private Task<HttpResponseMessage> Register(string email, string? referredBy = null) =>
        Post("/api/v1/auth/register", JsonSerializer.Serialize(new { email, password = Password, displayName = "Test User", referredBy }));

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

    private static string Validation(params string[] messages) =>
        $$$"""{"success":false,"error":{"statusCode":400,"message":{{{JsonSerializer.Serialize(messages)}}},"error":"Bad Request"}}""";

    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    private async Task<User> InsertUser(string email, string passwordHash, bool isActive = true)
    {
        var user = new User
        {
            Email = email, PasswordHash = passwordHash, DisplayName = "Fixture", IsActive = isActive,
            ReferralCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        };
        await using var db = factory.Db();
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user;
    }

    [Fact]
    public async Task Register_returns_201_with_user_tokens_and_creates_wallet()
    {
        var email = NewEmail();

        var data = await Data(await Register(email), HttpStatusCode.Created);

        Assert.Equal(["user", "accessToken", "refreshToken"], Keys(data));
        var user = data.GetProperty("user");
        Assert.Equal(["id", "email", "displayName", "role", "referralCode"], Keys(user));
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Test User", user.GetProperty("displayName").GetString());
        Assert.Equal("USER", user.GetProperty("role").GetString());
        Assert.Matches("^[A-Z0-9]{8}$", user.GetProperty("referralCode").GetString());

        await using var db = factory.Db();
        var saved = await db.Users.Include(u => u.Wallet).SingleAsync(u => u.Email == email, Ct);
        Assert.Equal(user.GetProperty("id").GetString(), saved.Id);
        Assert.StartsWith("$2b$12$", saved.PasswordHash);
        Assert.NotNull(saved.Wallet);
        Assert.Equal(0m, saved.Wallet.Balance);
        Assert.Equal(0m, saved.Wallet.LockedBalance);
        Assert.Equal("THB", saved.Wallet.Currency);
        Assert.Null(saved.ReferredBy);

        var me = await Data(await Send(HttpMethod.Get, "/api/v1/auth/me", data.GetProperty("accessToken").GetString()), HttpStatusCode.OK);
        Assert.Equal(saved.Id, me.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var email = NewEmail();
        await Data(await Register(email), HttpStatusCode.Created);

        await AssertBody(await Register(email), HttpStatusCode.Conflict,
            """{"success":false,"error":{"statusCode":409,"message":"Email already registered","error":"Conflict"}}""");
    }

    [Fact]
    public async Task Register_concurrent_duplicate_email_returns_one_201_and_one_409()
    {
        var email = NewEmail();

        var results = await Task.WhenAll(Register(email), Register(email), Register(email));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Register_with_referral_upserts_affiliate()
    {
        var referrer = await Data(await Register(NewEmail()), HttpStatusCode.Created);
        var referrerId = referrer.GetProperty("user").GetProperty("id").GetString();
        var code = referrer.GetProperty("user").GetProperty("referralCode").GetString();

        var first = await Data(await Register(NewEmail(), code), HttpStatusCode.Created);

        await using (var db = factory.Db())
        {
            var affiliate = await db.Affiliates.SingleAsync(a => a.UserId == referrerId, Ct);
            Assert.Equal(1, affiliate.TotalReferrals);
            Assert.Equal(2.00m, affiliate.CommissionRate);
            Assert.Equal(AffiliateStatus.ACTIVE, affiliate.Status);
            Assert.Equal(code, affiliate.ReferralCode);
            Assert.Equal(0m, affiliate.TotalEarnings);
            Assert.Equal(0m, affiliate.PendingEarnings);
            var referred = await db.Users.SingleAsync(u => u.Id == first.GetProperty("user").GetProperty("id").GetString(), Ct);
            Assert.Equal(referrerId, referred.ReferredBy);
        }

        await Data(await Register(NewEmail(), code), HttpStatusCode.Created);

        await using (var db = factory.Db())
            Assert.Equal(2, (await db.Affiliates.SingleAsync(a => a.UserId == referrerId, Ct)).TotalReferrals);
    }

    [Fact]
    public async Task Register_with_unknown_referral_code_ignores_it()
    {
        var data = await Data(await Register(NewEmail(), "NOPE0000"), HttpStatusCode.Created);

        await using var db = factory.Db();
        var user = await db.Users.SingleAsync(u => u.Id == data.GetProperty("user").GetProperty("id").GetString(), Ct);
        Assert.Null(user.ReferredBy);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Register_empty_body_lists_class_validator_messages_in_nest_order(string body) =>
        await AssertBody(await Post("/api/v1/auth/register", body), HttpStatusCode.BadRequest, Validation(
            "email must be an email",
            "password must contain uppercase, lowercase and number",
            "password must be shorter than or equal to 72 characters",
            "password must be longer than or equal to 8 characters",
            "password must be a string",
            "displayName must be shorter than or equal to 50 characters",
            "displayName must be longer than or equal to 2 characters",
            "displayName must be a string"));

    [Fact]
    public async Task Register_with_unknown_property_returns_400_property_should_not_exist() =>
        await AssertBody(
            await Post("/api/v1/auth/register", $$"""{"email":"{{NewEmail()}}","password":"{{Password}}","displayName":"Test","foo":1}"""),
            HttpStatusCode.BadRequest, Validation("property foo should not exist"));

    [Fact]
    public async Task Register_with_non_string_values_returns_must_be_a_string() =>
        await AssertBody(
            await Post("/api/v1/auth/register", """{"email":"a@b.co","password":123,"displayName":"ok","referredBy":5}"""),
            HttpStatusCode.BadRequest, Validation(
                "password must contain uppercase, lowercase and number",
                "password must be shorter than or equal to 72 characters",
                "password must be longer than or equal to 8 characters",
                "password must be a string",
                "referredBy must be a string"));

    [Theory]
    [InlineData("abcdefgh", "password must contain uppercase, lowercase and number")]
    [InlineData("Aa1", "password must be longer than or equal to 8 characters")]
    // class-validator counts code points: four emoji + "aA1" is 7 characters.
    [InlineData("\U0001F600\U0001F600\U0001F600\U0001F600aA1", "password must be longer than or equal to 8 characters")]
    [InlineData("Aa1xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "password must be shorter than or equal to 72 characters")]
    public async Task Register_rejects_weak_password(string password, string message) =>
        await AssertBody(
            await Post("/api/v1/auth/register", JsonSerializer.Serialize(new { email = NewEmail(), password, displayName = "Test" })),
            HttpStatusCode.BadRequest, Validation(message));

    [Theory]
    [InlineData("a@b.c")]
    [InlineData("a..b@x.com")]
    [InlineData("a@localhost")]
    [InlineData("a b@x.com")]
    public async Task Register_rejects_invalid_email(string email) =>
        await AssertBody(
            await Post("/api/v1/auth/register", JsonSerializer.Serialize(new { email, password = Password, displayName = "Test" })),
            HttpStatusCode.BadRequest, Validation("email must be an email"));

    [Fact]
    public async Task Login_returns_200_with_user_and_tokens_and_allows_unknown_properties()
    {
        var email = NewEmail();
        var registered = await Data(await Register(email), HttpStatusCode.Created);

        var data = await Data(
            await Post("/api/v1/auth/login", $$"""{"email":"{{email}}","password":"{{Password}}","foo":1}"""), HttpStatusCode.OK);

        Assert.Equal(["user", "accessToken", "refreshToken"], Keys(data));
        Assert.Equal(
            $$"""{"id":"{{registered.GetProperty("user").GetProperty("id").GetString()}}","email":"{{email}}","role":"USER"}""",
            data.GetProperty("user").GetRawText());
    }

    [Fact]
    public async Task Login_accepts_hash_created_by_node_bcrypt()
    {
        var email = NewEmail();
        var user = await InsertUser(email, NodeBcryptHash);

        var data = await Data(await Post("/api/v1/auth/login", JsonSerializer.Serialize(new { email, password = Password })), HttpStatusCode.OK);

        Assert.Equal(user.Id, data.GetProperty("user").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Login_wrong_password_returns_invalid_credentials()
    {
        var email = NewEmail();
        await InsertUser(email, NodeBcryptHash);

        await AssertBody(await Post("/api/v1/auth/login", JsonSerializer.Serialize(new { email, password = "wrongpass" })),
            HttpStatusCode.Unauthorized, InvalidCredentials);
    }

    [Fact]
    public async Task Login_unknown_email_returns_invalid_credentials() =>
        await AssertBody(await Post("/api/v1/auth/login", JsonSerializer.Serialize(new { email = NewEmail(), password = Password })),
            HttpStatusCode.Unauthorized, InvalidCredentials);

    [Fact]
    public async Task Login_inactive_user_returns_invalid_credentials()
    {
        var email = NewEmail();
        await InsertUser(email, NodeBcryptHash, isActive: false);

        await AssertBody(await Post("/api/v1/auth/login", JsonSerializer.Serialize(new { email, password = Password })),
            HttpStatusCode.Unauthorized, InvalidCredentials);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"email":"a@b.co"}""")]
    [InlineData("""{"password":"Test1234!"}""")]
    [InlineData("""{"email":"","password":"Test1234!"}""")]
    public async Task Login_missing_credentials_returns_bare_401(string body) =>
        await AssertBody(await Post("/api/v1/auth/login", body), HttpStatusCode.Unauthorized, BareUnauthorized);

    [Fact]
    public async Task Refresh_returns_new_token_pair()
    {
        var registered = await Data(await Register(NewEmail()), HttpStatusCode.Created);
        var refreshToken = registered.GetProperty("refreshToken").GetString();

        var data = await Data(await Post("/api/v1/auth/refresh", JsonSerializer.Serialize(new { refreshToken })), HttpStatusCode.OK);

        Assert.Equal(["accessToken", "refreshToken"], Keys(data));
        var me = await Data(await Send(HttpMethod.Get, "/api/v1/auth/me", data.GetProperty("accessToken").GetString()), HttpStatusCode.OK);
        Assert.Equal(registered.GetProperty("user").GetProperty("id").GetString(), me.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Refresh_with_access_token_or_garbage_returns_401()
    {
        var accessToken = (await Data(await Register(NewEmail()), HttpStatusCode.Created)).GetProperty("accessToken").GetString();
        const string expected =
            """{"success":false,"error":{"statusCode":401,"message":"Invalid or expired refresh token","error":"Unauthorized"}}""";

        await AssertBody(await Post("/api/v1/auth/refresh", JsonSerializer.Serialize(new { refreshToken = accessToken })),
            HttpStatusCode.Unauthorized, expected);
        await AssertBody(await Post("/api/v1/auth/refresh", """{"refreshToken":"not-a-real-token"}"""),
            HttpStatusCode.Unauthorized, expected);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"refreshToken":123}""")]
    public async Task Refresh_without_string_token_returns_400(string body) =>
        await AssertBody(await Post("/api/v1/auth/refresh", body), HttpStatusCode.BadRequest, Validation("refreshToken must be a string"));

    [Fact]
    public async Task Me_returns_token_claims()
    {
        var data = await Data(await Register(NewEmail()), HttpStatusCode.Created);
        var user = data.GetProperty("user");

        var me = await Data(await Send(HttpMethod.Get, "/api/v1/auth/me", data.GetProperty("accessToken").GetString()), HttpStatusCode.OK);

        Assert.Equal(
            $$"""{"id":"{{user.GetProperty("id").GetString()}}","email":"{{user.GetProperty("email").GetString()}}","role":"USER"}""",
            me.GetRawText());
    }

    [Fact]
    public async Task Me_and_logout_require_token()
    {
        await AssertBody(await Send(HttpMethod.Get, "/api/v1/auth/me", null), HttpStatusCode.Unauthorized, BareUnauthorized);
        await AssertBody(await Post("/api/v1/auth/logout", "{}"), HttpStatusCode.Unauthorized, BareUnauthorized);
    }

    [Fact]
    public async Task Logout_returns_201_message()
    {
        var token = (await Data(await Register(NewEmail()), HttpStatusCode.Created)).GetProperty("accessToken").GetString();

        await AssertBody(await Post("/api/v1/auth/logout", "{}", token), HttpStatusCode.Created,
            """{"success":true,"data":{"message":"Logged out successfully"}}""");
    }
}
