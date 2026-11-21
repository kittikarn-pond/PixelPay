using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace PixelPay.Infrastructure.Config;

/// <summary>Third-party credentials. All optional so the API boots without them; a call that needs one fails then.</summary>
public sealed record IntegrationsConfig(
    string PaymentGatewayUrl,
    string PaymentGatewayToken,
    string PaymentGatewaySecret,
    string SmtpHost,
    int SmtpPort,
    string SmtpUser,
    string SmtpPass,
    string SmtpFrom,
    string TwilioAccountSid,
    string TwilioAuthToken,
    string TwilioFrom)
{
    public static IntegrationsConfig Load(IConfiguration c) => new(
        Get(c, "PAYMENT_GATEWAY_URL", "https://api.gbprimepay.com"),
        Get(c, "PAYMENT_GATEWAY_API_KEY"),
        Get(c, "PAYMENT_GATEWAY_SECRET"),
        Get(c, "SMTP_HOST", "localhost"),
        int.TryParse(c["SMTP_PORT"], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : 1025,
        Get(c, "SMTP_USER"),
        Get(c, "SMTP_PASS"),
        Get(c, "SMTP_FROM", "noreply@pixelpay.local"),
        Get(c, "TWILIO_ACCOUNT_SID"),
        Get(c, "TWILIO_AUTH_TOKEN"),
        Get(c, "TWILIO_FROM"));

    private static string Get(IConfiguration c, string name, string fallback = "") =>
        string.IsNullOrWhiteSpace(c[name]) ? fallback : c[name]!;
}

/// <summary>redis://[:password@]host[:port][/db] to a StackExchange.Redis configuration string.</summary>
public static class RedisUrl
{
    public static string ToConfiguration(string url)
    {
        if (!url.Contains("://", StringComparison.Ordinal))
            return url;
        var uri = new Uri(url);
        var parts = new List<string> { $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}", "abortConnect=false" };
        if (uri.UserInfo.Length > 0)
        {
            var info = Uri.UnescapeDataString(uri.UserInfo);
            var colon = info.IndexOf(':');
            var user = colon >= 0 ? info[..colon] : "";
            var password = colon >= 0 ? info[(colon + 1)..] : info;
            if (user.Length > 0)
                parts.Add($"user={user}");
            if (password.Length > 0)
                parts.Add($"password={password}");
        }
        if (uri.Scheme == "rediss")
            parts.Add("ssl=true");
        if (uri.AbsolutePath.Trim('/') is { Length: > 0 } db)
            parts.Add($"defaultDatabase={db}");
        return string.Join(',', parts);
    }
}
