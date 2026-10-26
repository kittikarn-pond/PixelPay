using Microsoft.Extensions.Configuration;

namespace PixelPay.Infrastructure.Config;

public sealed record EnvConfig(
    string ConnectionString,
    string JwtSecret,
    string JwtRefreshSecret,
    string JwtExpiresIn,
    string JwtRefreshExpiresIn,
    string EncryptionKey,
    int Port,
    string FrontendUrl)
{
    public static EnvConfig Load(IConfiguration c) => new(
        MySqlUrl.ToConnectionString(Required(c, "DATABASE_URL")),
        Required(c, "JWT_SECRET"),
        Required(c, "JWT_REFRESH_SECRET"),
        Optional(c, "JWT_EXPIRES_IN", "15m"),
        Optional(c, "JWT_REFRESH_EXPIRES_IN", "7d"),
        Required(c, "ENCRYPTION_KEY"),
        int.Parse(Optional(c, "APP_PORT", "3000")),
        Optional(c, "FRONTEND_URL", "http://localhost:3001"));

    private static string Required(IConfiguration c, string name) =>
        string.IsNullOrWhiteSpace(c[name]) ? throw new InvalidOperationException($"{name} is required") : c[name]!;

    private static string Optional(IConfiguration c, string name, string fallback) =>
        string.IsNullOrWhiteSpace(c[name]) ? fallback : c[name]!;
}
