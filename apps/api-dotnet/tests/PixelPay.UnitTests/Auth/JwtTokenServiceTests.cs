using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PixelPay.Infrastructure.Auth;
using PixelPay.Infrastructure.Config;

namespace PixelPay.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private const string AccessSecret = "test_jwt_secret_64_chars_minimum_for_testing_purposes_only";
    private const string RefreshSecret = "test_refresh_secret_64_chars_minimum_for_testing_purposes";
    private const string UserId = "00000000-0000-0000-0000-000000000002";

    private static readonly JsonWebTokenHandler Handler = new();

    private static JwtTokenService Service(TimeProvider? time = null) => new(
        new EnvConfig("Server=x", AccessSecret, RefreshSecret, "15m", "7d", new string('0', 64), 3000, "http://x"),
        time ?? TimeProvider.System);

    private static string Sign(string secret, string payload, string alg = SecurityAlgorithms.HmacSha256) =>
        Handler.CreateToken(payload,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), alg));

    private static string Payload(long iat, long exp) =>
        JsonSerializer.Serialize(new { sub = UserId, email = "u@x.y", role = "USER", iat, exp });

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static async Task<TokenValidationResult> ValidateAccess(string token) =>
        await Handler.ValidateTokenAsync(token, JwtTokenService.ValidationParameters(AccessSecret));

    [Fact]
    public async Task Node_token_validates_with_role_claim_named_role()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Auth", "fixtures", "node-token.json");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var secret = fixture.RootElement.GetProperty("secret").GetString()!;
        var token = fixture.RootElement.GetProperty("token").GetString()!;

        var result = await Handler.ValidateTokenAsync(token, JwtTokenService.ValidationParameters(secret));

        Assert.True(result.IsValid, result.Exception?.Message);
        var identity = result.ClaimsIdentity;
        Assert.Equal("00000000-0000-0000-0000-000000000001", identity.Name);
        Assert.Equal("a@b.c", identity.FindFirst("email")?.Value);
        Assert.Equal("ADMIN", identity.FindFirst("role")?.Value);
        Assert.True(new System.Security.Claims.ClaimsPrincipal(identity).IsInRole("ADMIN"));
    }

    [Fact]
    public async Task Issued_access_token_has_only_sub_email_role_iat_exp_claims()
    {
        var pair = Service().Issue(UserId, "u@x.y", "USER");

        var jwt = new JsonWebToken(pair.AccessToken);
        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(["sub", "email", "role", "iat", "exp"], jwt.Claims.Select(c => c.Type));
        Assert.Equal(UserId, jwt.Subject);
        Assert.Equal("u@x.y", jwt.GetClaim("email").Value);
        Assert.Equal("USER", jwt.GetClaim("role").Value);
        Assert.Equal(900, jwt.GetPayloadValue<long>("exp") - jwt.GetPayloadValue<long>("iat"));
        Assert.True((await ValidateAccess(pair.AccessToken)).IsValid);
    }

    [Fact]
    public void Issued_refresh_token_lasts_refresh_ttl_and_validates()
    {
        var pair = Service().Issue(UserId, "u@x.y", "USER");

        var jwt = new JsonWebToken(pair.RefreshToken);
        Assert.Equal(7 * 86400, jwt.GetPayloadValue<long>("exp") - jwt.GetPayloadValue<long>("iat"));
        Assert.Equal((UserId, "u@x.y", "USER"), Service().ValidateRefresh(pair.RefreshToken));
    }

    [Fact]
    public void Refresh_token_signed_with_access_secret_is_rejected()
    {
        var pair = Service().Issue(UserId, "u@x.y", "USER");

        Assert.Null(Service().ValidateRefresh(pair.AccessToken));
        Assert.Null(Service().ValidateRefresh(Sign(AccessSecret, Payload(Now, Now + 600))));
    }

    [Fact]
    public async Task Refresh_token_is_not_an_access_token()
    {
        var pair = Service().Issue(UserId, "u@x.y", "USER");

        Assert.False((await ValidateAccess(pair.RefreshToken)).IsValid);
    }

    [Fact]
    public void Expired_refresh_token_is_rejected()
    {
        var past = new FixedTime(DateTimeOffset.UtcNow.AddDays(-7).AddSeconds(-1));
        var pair = Service(past).Issue(UserId, "u@x.y", "USER");

        Assert.Null(Service().ValidateRefresh(pair.RefreshToken));
    }

    [Fact]
    public async Task Alg_none_and_other_hmac_algorithms_are_rejected()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var unsigned = $"{header}.{Base64UrlEncoder.Encode(Payload(Now, Now + 600))}.";
        var hs512 = Sign(AccessSecret + AccessSecret, Payload(Now, Now + 600), SecurityAlgorithms.HmacSha512);

        Assert.False((await ValidateAccess(unsigned)).IsValid);
        Assert.False((await Handler.ValidateTokenAsync(hs512,
            JwtTokenService.ValidationParameters(AccessSecret + AccessSecret))).IsValid);
        Assert.Null(Service().ValidateRefresh("not-a-token"));
    }

    [Fact]
    public async Task No_clock_skew()
    {
        var expired = Sign(AccessSecret, Payload(Now - 60, Now - 1));

        Assert.False((await ValidateAccess(expired)).IsValid);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
