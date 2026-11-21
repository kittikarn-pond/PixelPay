using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PixelPay.Application.Auth;
using PixelPay.Infrastructure.Config;

namespace PixelPay.Infrastructure.Auth;

/// <summary>HS256 tokens interchangeable with the Node API's jsonwebtoken: payload {sub,email,role,iat,exp}.</summary>
public sealed class JwtTokenService(EnvConfig env, TimeProvider time) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    private readonly TimeSpan _accessTtl = DurationParser.Parse(env.JwtExpiresIn);
    private readonly TimeSpan _refreshTtl = DurationParser.Parse(env.JwtRefreshExpiresIn);
    private readonly SigningCredentials _accessCredentials = Credentials(env.JwtSecret);
    private readonly SigningCredentials _refreshCredentials = Credentials(env.JwtRefreshSecret);
    private readonly TokenValidationParameters _refreshValidation = ValidationParameters(env.JwtRefreshSecret);

    public static TokenValidationParameters ValidationParameters(string secret) => new()
    {
        IssuerSigningKey = Key(secret),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidateIssuer = false,
        ValidateAudience = false,
        NameClaimType = "sub",
        RoleClaimType = "role",
    };

    public TokenPair Issue(string userId, string email, string role)
    {
        var iat = time.GetUtcNow().ToUnixTimeSeconds();
        return new TokenPair(
            Sign(userId, email, role, iat, _accessTtl, _accessCredentials),
            Sign(userId, email, role, iat, _refreshTtl, _refreshCredentials));
    }

    public (string Sub, string Email, string Role)? ValidateRefresh(string token)
    {
        // Symmetric key, no metadata endpoint: validation completes synchronously.
        var result = Handler.ValidateTokenAsync(token, _refreshValidation).GetAwaiter().GetResult();
        if (!result.IsValid)
            return null;

        var claims = result.ClaimsIdentity;
        var sub = claims.FindFirst("sub")?.Value;
        var email = claims.FindFirst("email")?.Value;
        var role = claims.FindFirst("role")?.Value;
        return sub is null || email is null || role is null ? null : (sub, email, role);
    }

    private static string Sign(string sub, string email, string role, long iat, TimeSpan ttl, SigningCredentials credentials) =>
        Handler.CreateToken(
            JsonSerializer.Serialize(new { sub, email, role, iat, exp = iat + (long)ttl.TotalSeconds }),
            credentials);

    private static SymmetricSecurityKey Key(string secret) => new(Encoding.UTF8.GetBytes(secret));

    private static SigningCredentials Credentials(string secret) => new(Key(secret), SecurityAlgorithms.HmacSha256);
}
