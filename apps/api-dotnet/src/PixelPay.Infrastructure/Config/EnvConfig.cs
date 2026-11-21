using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using PixelPay.Infrastructure.Auth;

namespace PixelPay.Infrastructure.Config;

public sealed record EnvConfig(
    string ConnectionString,
    string JwtSecret,
    string JwtRefreshSecret,
    string JwtExpiresIn,
    string JwtRefreshExpiresIn,
    string EncryptionKey,
    int Port,
    string FrontendUrl,
    string RedisUrl)
{
    public static EnvConfig Load(IConfiguration c) => new(
        MySqlUrl.ToConnectionString(Required(c, "DATABASE_URL")),
        Secret(c, "JWT_SECRET"),
        Secret(c, "JWT_REFRESH_SECRET"),
        Duration(c, "JWT_EXPIRES_IN", "15m"),
        Duration(c, "JWT_REFRESH_EXPIRES_IN", "7d"),
        Required(c, "ENCRYPTION_KEY"),
        ParsePort(c),
        Optional(c, "FRONTEND_URL", "http://localhost:3001"),
        Optional(c, "REDIS_URL", "redis://localhost:6379"));

    private static int ParsePort(IConfiguration c) =>
        int.TryParse(Optional(c, "APP_PORT", "3000"), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            ? port
            : throw new InvalidOperationException("APP_PORT must be an integer");

    private static string Secret(IConfiguration c, string name)
    {
        var v = Required(c, name);
        return Encoding.UTF8.GetByteCount(v) >= 32 ? v : throw new InvalidOperationException($"{name} must be at least 32 bytes");
    }

    private static string Duration(IConfiguration c, string name, string fallback)
    {
        var v = Optional(c, name, fallback);
        try
        {
            DurationParser.Parse(v);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{name} is not a valid duration");
        }
        return v;
    }

    private static string Required(IConfiguration c, string name) =>
        string.IsNullOrWhiteSpace(c[name]) ? throw new InvalidOperationException($"{name} is required") : c[name]!;

    private static string Optional(IConfiguration c, string name, string fallback) =>
        string.IsNullOrWhiteSpace(c[name]) ? fallback : c[name]!;
}
