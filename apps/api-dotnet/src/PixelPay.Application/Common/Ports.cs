namespace PixelPay.Application.Common;

/// <summary>Background work, run by PixelPay.Worker (Hangfire queues "topup", "payments", "notifications").</summary>
public interface IJobQueue
{
    void EnqueueTopup(string orderId, string userId, string gameProductId);

    /// <summary>Checks a PromptPay charge after <paramref name="delay"/>; <paramref name="attempt"/> starts at 1.</summary>
    void SchedulePaymentPoll(string paymentId, string chargeId, int attempt, TimeSpan delay);

    void EnqueueNotification(string notificationId);
}

/// <summary>Live order status to the buyer's browser (SignalR group <c>user:{id}</c>, event <c>order.status</c>).</summary>
public interface IOrderStatusNotifier
{
    Task NotifyAsync(string userId, string orderId, string status, CancellationToken ct = default);
}

public sealed record QrCharge(string ChargeId, string QrCodeUrl, DateTime ExpiresAt, decimal Amount);

/// <summary>GB Prime Pay QR Cash.</summary>
public interface IPaymentGateway
{
    Task<QrCharge> CreatePromptPayQrAsync(decimal amount, CancellationToken ct);

    /// <summary>True once the charge is paid ("S"); anything else counts as still pending.</summary>
    Task<bool> IsPaidAsync(string chargeId, CancellationToken ct);
}

public sealed record TopupRequest(
    string ProviderSku, string GameUid, string? GameServer, string? GameUsername, string OrderId, int Quantity);

public sealed record TopupResult(bool Success, string? ProviderOrderId = null, string? ProviderReference = null,
    string? FailureReason = null);

public interface ITopupProvider
{
    string Slug { get; }

    /// <summary>Never throws for provider or network errors: those come back as a failed result.</summary>
    Task<TopupResult> ProcessTopupAsync(TopupRequest request, CancellationToken ct);
}

public sealed record ProviderCredentials(string Slug, string ApiUrl, string ApiKeyEnc, string? ApiSecretEnc);

public interface ITopupProviderFactory
{
    ITopupProvider Create(ProviderCredentials provider);
}

/// <summary>AES-256-GCM with ENCRYPTION_KEY, in the iv:tag:data hex format the database already holds.</summary>
public interface ISecretBox
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct);
}

public interface ISmsSender
{
    /// <summary>Logs and swallows failures: an SMS problem must not break the order flow.</summary>
    Task SendAsync(string to, string body, CancellationToken ct);
}
