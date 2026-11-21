using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure.Security;

namespace PixelPay.Infrastructure.External;

public sealed class SecretBox(EnvConfig env) : ISecretBox
{
    public string Encrypt(string plaintext) => CryptoUtil.EncryptAes256(env.EncryptionKey, plaintext);

    public string Decrypt(string ciphertext) => CryptoUtil.DecryptAes256(env.EncryptionKey, ciphertext);
}

/// <summary>
/// GB Prime Pay QR Cash. PAYMENT_GATEWAY_API_KEY is the "token" (Profile > Gen Token) sent when creating the QR;
/// PAYMENT_GATEWAY_SECRET is the merchant secret key, used only as Basic auth on the status-query API.
/// </summary>
public sealed class GbPrimePayGateway(HttpClient http, IntegrationsConfig config, TimeProvider clock) : IPaymentGateway
{
    public async Task<QrCharge> CreatePromptPayQrAsync(decimal amount, CancellationToken ct)
    {
        // referenceNo: unique per charge and at most 15 chars, so not our Payment.id.
        var referenceNo = $"{clock.GetUtcNow().ToUnixTimeMilliseconds()}{Random.Shared.Next(100):D2}";
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = config.PaymentGatewayToken,
            ["amount"] = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["referenceNo"] = referenceNo,
            ["detail"] = "PixelPay wallet top-up",
        });
        using var response = await http.PostAsync("/v3/qrcode", body, ct);
        response.EnsureSuccessStatusCode();
        var png = await response.Content.ReadAsByteArrayAsync(ct);

        return new QrCharge(referenceNo, $"data:image/png;base64,{Convert.ToBase64String(png)}",
            clock.GetUtcNow().UtcDateTime.AddMinutes(15), amount);
    }

    public async Task<bool> IsPaidAsync(string chargeId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/check_status_txn")
        {
            Content = JsonContent.Create(new { referenceNo = chargeId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.PaymentGatewaySecret}:")));
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        // "S" is the only documented paid/settled code; anything else is treated as still pending.
        return json?["txn"]?["status"]?.GetValue<string>() == "S";
    }
}

public sealed class TopupProviderFactory(IHttpClientFactory http, ISecretBox secrets, TimeProvider clock,
    ILoggerFactory logs) : ITopupProviderFactory
{
    public ITopupProvider Create(ProviderCredentials provider)
    {
        var apiKey = secrets.Decrypt(provider.ApiKeyEnc);
        var apiSecret = provider.ApiSecretEnc is null ? "" : secrets.Decrypt(provider.ApiSecretEnc);
        var client = http.CreateClient("providers");
        return provider.Slug switch
        {
            "unipin" => new UnipinProvider(client, provider.ApiUrl, apiKey, apiSecret, clock, logs.CreateLogger<UnipinProvider>()),
            "smileone" => new SmileOneProvider(client, provider.ApiUrl, apiKey, apiSecret, clock, logs.CreateLogger<SmileOneProvider>()),
            _ => throw new InvalidOperationException($"No adapter for provider slug: {provider.Slug}"),
        };
    }
}

public sealed class SmileOneProvider(HttpClient http, string apiUrl, string apiKey, string apiSecret, TimeProvider clock,
    ILogger log) : ITopupProvider
{
    public string Slug => "smileone";

    public async Task<TopupResult> ProcessTopupAsync(TopupRequest r, CancellationToken ct)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["apiKey"] = apiKey,
            ["orderId"] = r.OrderId,
            ["productCode"] = r.ProviderSku,
            ["userId"] = r.GameUid,
            ["zoneId"] = r.GameServer ?? "",
            ["qty"] = r.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["timestamp"] = clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        // Signature: HMAC-SHA256 over the fields sorted by key as k=v joined with &.
        var sign = CryptoUtil.HmacSha256Hex(apiSecret, string.Join('&', fields.Select(kv => $"{kv.Key}={kv.Value}")));
        var body = new Dictionary<string, string>(fields) { ["sign"] = sign };

        try
        {
            var data = await PostJson(http, $"{apiUrl}/order", body, ct);
            if (Num(data, "code") == 200 || Str(data, "status") == "SUCCESS")
                return new TopupResult(true, Str(data, "orderId") ?? Str(data, "tradeNo"), Str(data, "serialNo"));
            return new TopupResult(false, FailureReason: Str(data, "msg") ?? "Unknown error");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError("Smile.one topup failed: {Message}", ex.Message);
            return new TopupResult(false, FailureReason: ex.Message);
        }
    }

    internal static async Task<JsonNode?> PostJson(HttpClient http, string url, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonNode>(ct);
    }

    internal static string? Str(JsonNode? data, string key) => data?[key] is JsonValue v && v.TryGetValue<string>(out var s)
        ? s
        : data?[key] is JsonValue n && n.GetValueKind() == JsonValueKind.Number ? n.ToJsonString() : null;

    internal static double? Num(JsonNode? data, string key) =>
        data?[key] is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>() : null;
}

public sealed class UnipinProvider(HttpClient http, string apiUrl, string apiKey, string apiSecret, TimeProvider clock,
    ILogger log) : ITopupProvider
{
    public string Slug => "unipin";

    public async Task<TopupResult> ProcessTopupAsync(TopupRequest r, CancellationToken ct)
    {
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = CryptoUtil.HmacSha256Hex(apiSecret, $"{apiKey}{r.OrderId}{r.ProviderSku}{r.GameUid}{timestamp}");
        var body = new
        {
            partnerTrxId = r.OrderId,
            itemCode = r.ProviderSku,
            userId = r.GameUid,
            serverId = r.GameServer ?? "",
            qty = r.Quantity,
            timestamp,
            signature,
            apiKey,
        };

        try
        {
            var data = await SmileOneProvider.PostJson(http, $"{apiUrl}/topup", body, ct);
            if (SmileOneProvider.Str(data, "status") == "success" || SmileOneProvider.Str(data, "rc") == "00")
                return new TopupResult(true,
                    SmileOneProvider.Str(data, "trxId") ?? SmileOneProvider.Str(data, "orderId"),
                    SmileOneProvider.Str(data, "serialNumber") ?? SmileOneProvider.Str(data, "ref"));
            return new TopupResult(false, FailureReason: SmileOneProvider.Str(data, "message") ?? "Provider returned failure");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError("Unipin topup failed: {Message}", ex.Message);
            return new TopupResult(false, FailureReason: ex.Message);
        }
    }
}

public sealed class SmtpEmailSender(IntegrationsConfig config, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config.SmtpFrom));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = html };

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(config.SmtpHost, config.SmtpPort, SecureSocketOptions.Auto, ct);
            if (config.SmtpUser.Length > 0)
                await smtp.AuthenticateAsync(config.SmtpUser, config.SmtpPass, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Email send failed to {To}: {Message}", to, ex.Message);
            throw; // the dispatch job retries
        }
    }
}

public sealed class TwilioSmsSender(HttpClient http, IntegrationsConfig config, ILogger<TwilioSmsSender> log) : ISmsSender
{
    public async Task SendAsync(string to, string body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(config.TwilioAccountSid)}/Messages.json")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["From"] = config.TwilioFrom,
                    ["To"] = to,
                    ["Body"] = body,
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.TwilioAccountSid}:{config.TwilioAuthToken}")));
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never rethrow: an SMS failure must not break the order flow.
            log.LogError("SMS send failed to {To}: {Message}", to, ex.Message);
        }
    }
}
