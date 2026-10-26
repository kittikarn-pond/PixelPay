using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PixelPay.Application.Auth;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Auth;

public class GuardTests : IClassFixture<ApiFactory>
{
    private const string Unauthorized = """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""";
    private const string Forbidden =
        """{"success":false,"error":{"statusCode":403,"message":"Forbidden resource","error":"Forbidden"}}""";
    private const string AccessSecret = "test_jwt_secret_64_chars_minimum_for_testing_purposes_only";

    private readonly HttpClient _client;
    private readonly ITokenService _tokens;

    public GuardTests(ApiFactory factory)
    {
        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddControllers().ConfigureApplicationPartManager(m =>
                m.ApplicationParts.Add(new AssemblyPart(typeof(AuthTestController).Assembly)))));
        _client = app.CreateClient();
        _tokens = app.Services.GetRequiredService<ITokenService>();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task AssertResponse(string url, string? token, HttpStatusCode status, string body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(req, Ct);
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(body, await res.Content.ReadAsStringAsync(Ct));
    }

    private static string NodeToken()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Auth", "fixtures", "node-token.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private static string ExpiredToken()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = JsonSerializer.Serialize(new { sub = "u1", email = "u@x.y", role = "ADMIN", iat = now - 60, exp = now - 1 });
        return new JsonWebTokenHandler().CreateToken(payload,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccessSecret)), SecurityAlgorithms.HmacSha256));
    }

    [Fact]
    public Task No_token_is_401() =>
        AssertResponse("/api/v1/auth-test/me", null, HttpStatusCode.Unauthorized, Unauthorized);

    [Fact]
    public Task No_token_on_admin_action_is_401() =>
        AssertResponse("/api/v1/auth-test/admin", null, HttpStatusCode.Unauthorized, Unauthorized);

    [Fact]
    public Task Token_expired_one_second_ago_is_401() =>
        AssertResponse("/api/v1/auth-test/me", ExpiredToken(), HttpStatusCode.Unauthorized, Unauthorized);

    [Fact]
    public Task Garbage_token_is_401() =>
        AssertResponse("/api/v1/auth-test/me", "not.a.jwt", HttpStatusCode.Unauthorized, Unauthorized);

    [Fact]
    public Task Refresh_token_as_bearer_is_401() =>
        AssertResponse("/api/v1/auth-test/me", _tokens.Issue("u1", "u@x.y", "USER").RefreshToken,
            HttpStatusCode.Unauthorized, Unauthorized);

    [Fact]
    public Task User_token_on_admin_action_is_403() =>
        AssertResponse("/api/v1/auth-test/admin", _tokens.Issue("u1", "u@x.y", "USER").AccessToken,
            HttpStatusCode.Forbidden, Forbidden);

    [Fact]
    public Task Issued_access_token_binds_current_user() =>
        AssertResponse("/api/v1/auth-test/me", _tokens.Issue("u1", "u@x.y", "USER").AccessToken,
            HttpStatusCode.OK, """{"success":true,"data":{"id":"u1","email":"u@x.y","role":"USER"}}""");

    [Fact]
    public Task Node_token_passes_admin_guard() =>
        AssertResponse("/api/v1/auth-test/admin", NodeToken(), HttpStatusCode.OK, """{"success":true,"data":{"ok":true}}""");

    [Fact]
    public Task Node_token_binds_current_user() =>
        AssertResponse("/api/v1/auth-test/me", NodeToken(), HttpStatusCode.OK,
            """{"success":true,"data":{"id":"00000000-0000-0000-0000-000000000001","email":"a@b.c","role":"ADMIN"}}""");
}
