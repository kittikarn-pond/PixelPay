using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PixelPay.Application.Common;
using PixelPay.Application.Wallets;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Payments;

public sealed record CreatePromptPayDto(JsonElement Amount);

public sealed class CreatePromptPayDtoValidator : DtoValidator<CreatePromptPayDto>
{
    public CreatePromptPayDtoValidator() => Field(x => x.Amount, "amount").Number().Min(10).Max(100000);
}

public sealed class PaymentsService(
    IAppDbContext db, IPaymentGateway gateway, IJobQueue jobs, PaymentSettlement settlement, ILogger<PaymentsService> log)
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public async Task<Dictionary<string, object?>> CreatePromptPayAsync(string userId, CreatePromptPayDto dto, CancellationToken ct)
    {
        var amount = dto.Amount.Dec() ?? throw new InvalidOperationException("amount out of range");
        var payment = new Payment
        {
            UserId = userId,
            PaymentMethod = PaymentMethod.PROMPTPAY,
            Amount = amount,
            Currency = "THB",
            Status = PaymentStatus.PENDING,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        try
        {
            var qr = await gateway.CreatePromptPayQrAsync(amount, ct);
            payment.GatewayReference = qr.ChargeId;
            payment.QrCodeUrl = qr.QrCodeUrl;
            payment.ExpiresAt = PrismaTime.Truncate(qr.ExpiresAt);
            await db.SaveChangesAsync(ct);

            jobs.SchedulePaymentPoll(payment.Id, qr.ChargeId, attempt: 1, PollInterval);
            return Rows.Of(payment);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            payment.Status = PaymentStatus.FAILED;
            await db.SaveChangesAsync(CancellationToken.None);
            log.LogError(ex, "Failed to create PromptPay QR for payment {PaymentId}", payment.Id);
            throw new BadRequestException("Failed to create payment. Please try again.");
        }
    }

    public async Task<Dictionary<string, object?>> GetAsync(string id, string userId, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Payment not found");
        if (payment.UserId != userId)
            throw new UnauthorizedException();
        return Rows.Of(payment);
    }

    /// <summary>
    /// GB Prime Pay's QR Cash callback is unsigned, so its body only says which charge to re-check;
    /// whether to credit is decided by our own authenticated status query.
    /// </summary>
    public async Task<object> HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        string? referenceNo;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            referenceNo = doc.RootElement.ValueKind == JsonValueKind.Object
                          && doc.RootElement.TryGetProperty("referenceNo", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString()
                : null;
        }
        catch (JsonException)
        {
            throw new BadRequestException("Invalid webhook payload");
        }
        if (string.IsNullOrEmpty(referenceNo))
            throw new BadRequestException("Missing referenceNo");

        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.GatewayReference == referenceNo, ct);
        if (payment is null || payment.Status != PaymentStatus.PENDING)
            return new { received = true };

        if (await gateway.IsPaidAsync(referenceNo, ct))
            await settlement.SettleAsync(payment, "PromptPay deposit", ct);
        return new { received = true };
    }
}

/// <summary>Marks a pending payment COMPLETED and credits the wallet, exactly once.</summary>
public sealed class PaymentSettlement(IAppDbContext db, WalletService wallet, ILogger<PaymentSettlement> log)
{
    public async Task SettleAsync(Payment payment, string description, CancellationToken ct)
    {
        var credited = await Tx.RunAsync(db, async () =>
        {
            var now = PrismaTime.Now();
            // The PENDING guard makes the webhook and the poller race-safe: only one of them credits.
            var updated = await db.Payments.Where(p => p.Id == payment.Id && p.Status == PaymentStatus.PENDING)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.COMPLETED).SetProperty(p => p.PaidAt, now), ct);
            if (updated == 0)
                return false;

            var walletId = await wallet.WalletIdForUserAsync(payment.UserId, ct);
            await wallet.CreditAsync(walletId, payment.Amount, WalletTransactionType.DEPOSIT, description, payment.Id, "PAYMENT", ct);
            return true;
        }, ct);

        if (credited)
            log.LogInformation("Wallet credited {Amount} for user {UserId} (payment {PaymentId})",
                payment.Amount, payment.UserId, payment.Id);
    }
}

/// <summary>
/// Polls one PromptPay charge every 10 s for up to 90 attempts (15 min), then expires it.
/// GB Prime Pay never reports a QR as expired or failed, so this is what closes abandoned payments.
/// </summary>
public sealed class PaymentPollJob(IAppDbContext db, IPaymentGateway gateway, IJobQueue jobs, PaymentSettlement settlement,
    TimeProvider clock, ILogger<PaymentPollJob> log)
{
    public const int MaxAttempts = 90;

    public async Task RunAsync(string paymentId, string chargeId, int attempt, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.PENDING)
            return;

        if (payment.ExpiresAt is { } expires && expires < clock.GetUtcNow().UtcDateTime)
        {
            await ExpireAsync(paymentId, ct);
            return;
        }

        bool paid;
        try
        {
            paid = await gateway.IsPaidAsync(chargeId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed status call is one used-up attempt, as in Node; the next poll (or the webhook) tries again.
            log.LogWarning(ex, "Status check failed for payment {PaymentId} (attempt {Attempt})", paymentId, attempt);
            paid = false;
        }

        if (paid)
        {
            await settlement.SettleAsync(payment, "PromptPay deposit via poller", ct);
            return;
        }

        if (attempt >= MaxAttempts)
            await ExpireAsync(paymentId, ct);
        else
            jobs.SchedulePaymentPoll(paymentId, chargeId, attempt + 1, PaymentsService.PollInterval);
    }

    private Task ExpireAsync(string paymentId, CancellationToken ct) =>
        db.Payments.Where(p => p.Id == paymentId && p.Status == PaymentStatus.PENDING)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.EXPIRED), ct);
}
