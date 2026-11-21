using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Notifications;

public sealed record SendNotification(
    string UserId, string Type, NotificationChannel Channel, string Title, string Body,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed class NotificationsService(IAppDbContext db, IJobQueue jobs)
{
    /// <summary>Stores the notification, then queues its delivery.</summary>
    public async Task<Notification> SendAsync(SendNotification dto, CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = dto.UserId,
            Type = dto.Type,
            Channel = dto.Channel,
            Title = dto.Title,
            Body = dto.Body,
            Metadata = new RawJson(JsonSerializer.Serialize(dto.Metadata ?? new Dictionary<string, object?>())),
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        jobs.EnqueueNotification(notification.Id);
        return notification;
    }

    public async Task<object> FindByUserAsync(string userId, double page, double limit, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var items = await PrismaPaging.NewestFirstAsync(query, n => n.CreatedAt, page, limit, ct);
        var total = await query.CountAsync(ct);
        var unread = await query.CountAsync(n => !n.IsRead, ct);
        return new { items = items.Select(n => Rows.Of(n)), total, unread, page, limit };
    }

    /// <summary>Prisma updateMany result shape: <c>{ count }</c>.</summary>
    public async Task<object> MarkReadAsync(string id, string userId, CancellationToken ct) =>
        new { count = await db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct) };

    public async Task<object> MarkAllReadAsync(string userId, CancellationToken ct) =>
        new { count = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct) };
}

/// <summary>Delivers one stored notification over its channel (queue "notifications").</summary>
public sealed class NotificationDispatchJob(IAppDbContext db, IEmailSender email, ISmsSender sms)
{
    public async Task RunAsync(string notificationId, CancellationToken ct)
    {
        var n = await db.Notifications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (n is null)
            return;

        var meta = Meta(n.Metadata);
        if (n.Channel == NotificationChannel.EMAIL && !string.IsNullOrEmpty(n.User.Email))
        {
            var template = Templates.Email(n.Type, n.User.DisplayName, meta);
            await email.SendAsync(n.User.Email, template?.Subject ?? n.Title, template?.Html ?? $"<p>{Html(n.Body)}</p>", ct);
        }
        else if (n.Channel == NotificationChannel.SMS && !string.IsNullOrEmpty(n.User.Phone))
        {
            await sms.SendAsync(n.User.Phone, Templates.Sms(n.Type, meta) ?? n.Body, ct);
        }
        // IN_APP: the stored row is the delivery.

        n.SentAt = PrismaTime.Now();
        await db.SaveChangesAsync(ct);
    }

    private static Dictionary<string, string> Meta(RawJson? json)
    {
        var result = new Dictionary<string, string>();
        if (json is not { } raw)
            return result;
        using var doc = JsonDocument.Parse(raw.Value);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var p in doc.RootElement.EnumerateObject())
            if (p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                result[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText();
        return result;
    }

    private static string Html(string s) => WebUtility.HtmlEncode(s);
}

internal static class Templates
{
    public sealed record EmailTemplate(string Subject, string Html);

    // Values are HTML-encoded: displayName and gameUid are user input.
    public static EmailTemplate? Email(string type, string displayName, IReadOnlyDictionary<string, string> m)
    {
        string V(string key) => WebUtility.HtmlEncode(m.GetValueOrDefault(key, "undefined"));
        var name = WebUtility.HtmlEncode(displayName);
        return type switch
        {
            "ORDER_COMPLETED" => new($"[PixelPay] Order {m.GetValueOrDefault("orderNumber")} Completed", $"""
                <h2>Hi {name},</h2>
                <p>Your order <strong>{V("orderNumber")}</strong> has been completed successfully!</p>
                <p><strong>Product:</strong> {V("productName")}<br>
                <strong>Game UID:</strong> {V("gameUid")}</p>
                <p>Thank you for using PixelPay!</p>
                """),
            "ORDER_FAILED" => new($"[PixelPay] Order {m.GetValueOrDefault("orderNumber")} Failed", $"""
                <h2>Hi {name},</h2>
                <p>Unfortunately, your order <strong>{V("orderNumber")}</strong> for <strong>{V("productName")}</strong> could not be completed.</p>
                <p>Your wallet balance has been refunded. Please try again or contact support.</p>
                """),
            "PAYMENT_RECEIVED" => new("[PixelPay] Wallet Top-Up Successful", $"""
                <h2>Hi {name},</h2>
                <p>Your wallet has been topped up with <strong>{V("amount")} THB</strong>.</p>
                <p>Your balance is now ready to use on PixelPay.</p>
                """),
            _ => null,
        };
    }

    public static string? Sms(string type, IReadOnlyDictionary<string, string> m) => type switch
    {
        "ORDER_COMPLETED" =>
            $"[PixelPay] Order {m.GetValueOrDefault("orderNumber")} completed: {m.GetValueOrDefault("productName")}. Thank you!",
        "ORDER_FAILED" => $"[PixelPay] Order {m.GetValueOrDefault("orderNumber")} failed. Your balance has been refunded.",
        _ => null,
    };
}
