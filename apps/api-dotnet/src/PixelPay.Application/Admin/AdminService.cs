using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;
using PixelPay.Application.Wallets;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Admin;

public sealed record UpdateUserDto(JsonElement IsActive, JsonElement Role);

public sealed record OverrideStatusDto(JsonElement Status);

public sealed record CreateProviderDto(
    JsonElement Name, JsonElement Slug, JsonElement ApiUrl, JsonElement ApiKey, JsonElement ApiSecret, JsonElement Priority,
    JsonElement IsActive, JsonElement RateLimitPerMin, JsonElement HealthCheckUrl);

public sealed record UpdateProviderDto(
    JsonElement Name, JsonElement ApiUrl, JsonElement ApiKey, JsonElement ApiSecret, JsonElement Priority,
    JsonElement IsActive, JsonElement RateLimitPerMin, JsonElement HealthCheckUrl);

public sealed class CreateProviderDtoValidator : DtoValidator<CreateProviderDto>
{
    // Only slugs with a provider adapter: anything else would save but fail every top-up routed to it.
    public CreateProviderDtoValidator()
    {
        Field(x => x.Name, "name").String().MaxLength(100);
        Field(x => x.Slug, "slug").In("smileone", "unipin");
        Field(x => x.ApiUrl, "apiUrl").Url(requireTld: false);
        Field(x => x.ApiKey, "apiKey").String().MaxLength(500);
        Field(x => x.ApiSecret, "apiSecret").Optional().String().MaxLength(500);
        Field(x => x.Priority, "priority").Optional().Int().Min(1).Max(100);
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.RateLimitPerMin, "rateLimitPerMin").Optional().Int().Min(1);
        Field(x => x.HealthCheckUrl, "healthCheckUrl").IfTruthy().Url(requireTld: false);
    }
}

public sealed class UpdateProviderDtoValidator : DtoValidator<UpdateProviderDto>
{
    public UpdateProviderDtoValidator()
    {
        Field(x => x.Name, "name").Optional().String().MaxLength(100);
        Field(x => x.ApiUrl, "apiUrl").Optional().Url(requireTld: false);
        Field(x => x.ApiKey, "apiKey").Optional().String().MaxLength(500);
        Field(x => x.ApiSecret, "apiSecret").Optional().String().MaxLength(500);
        Field(x => x.Priority, "priority").Optional().Int().Min(1).Max(100);
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.RateLimitPerMin, "rateLimitPerMin").Optional().Int().Min(1);
        Field(x => x.HealthCheckUrl, "healthCheckUrl").IfTruthy().Url(requireTld: false);
    }
}

public sealed class AdminService(IAppDbContext db, WalletService wallet, IJobQueue jobs, ISecretBox secrets, TimeProvider clock)
{
    private static readonly string[] UserColumns =
        ["id", "email", "phone", "displayName", "avatarUrl", "role", "isVerified", "isActive", "referralCode", "createdAt"];

    // apiKeyEnc / apiSecretEnc are never sent to any client.
    private static readonly string[] ProviderColumns =
        ["id", "name", "slug", "apiUrl", "priority", "isActive", "rateLimitPerMin", "healthCheckUrl", "createdAt", "updatedAt"];

    public async Task<object> GetDashboardStatsAsync(CancellationToken ct)
    {
        // Day and month boundaries in the server's time zone, as Node computed them.
        var local = clock.GetLocalNow();
        var startOfDay = new DateTimeOffset(local.Date, local.Offset).UtcDateTime;
        var startOfMonth = new DateTimeOffset(new DateTime(local.Year, local.Month, 1), local.Offset).UtcDateTime;

        var revenue = await db.Orders.Where(o => o.Status == OrderStatus.COMPLETED).SumAsync(o => (decimal?)o.TotalPrice, ct);
        return new
        {
            totalUsers = await db.Users.CountAsync(ct),
            totalOrders = await db.Orders.CountAsync(ct),
            pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.PENDING, ct),
            todayOrders = await db.Orders.CountAsync(o => o.CreatedAt >= startOfDay, ct),
            monthOrders = await db.Orders.CountAsync(o => o.CreatedAt >= startOfMonth, ct),
            // Prisma's _sum is a Decimal (a string in JSON), or 0 when nothing matched.
            totalRevenue = revenue is { } r ? (object)r : 0,
        };
    }

    public async Task<object> ListUsersAsync(double page, double limit, string? search, CancellationToken ct)
    {
        IQueryable<User> query = db.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Email.Contains(search) || u.DisplayName.Contains(search));
        var items = await PrismaPaging.NewestFirstAsync(query, u => u.CreatedAt, page, limit, ct);
        var total = await query.CountAsync(ct);
        return new { items = items.Select(u => Rows.Pick(u, UserColumns)), total, page, limit };
    }

    public async Task<Dictionary<string, object?>> UpdateUserAsync(string id, UpdateUserDto dto, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User not found");
        dto.IsActive.Set(e => e.Bool() ?? throw new BadRequestException("isActive must be a boolean value"), v => user.IsActive = v);
        dto.Role.Set(e => Enum.TryParse<UserRole>(e.Str(), out var r) && Enum.IsDefined(r) ? r
            : throw new BadRequestException("Invalid role"), v => user.Role = v);
        await db.SaveChangesAsync(ct);
        return Rows.Pick(user, UserColumns);
    }

    public async Task<object> ListOrdersAsync(double page, double limit, string? status, string? userId, CancellationToken ct)
    {
        IQueryable<Order> query = db.Orders.AsNoTracking().Include(o => o.User).Include(o => o.GameProduct);
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, out var s) || !Enum.IsDefined(s) || status != s.ToString())
                throw new BadRequestException($"Invalid status: {status}");
            query = query.Where(o => o.Status == s);
        }
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(o => o.UserId == userId);

        var items = await PrismaPaging.NewestFirstAsync(query, o => o.CreatedAt, page, limit, ct);
        var total = await query.CountAsync(ct);
        return new
        {
            items = items.Select(o => Rows.Of(o)
                .With("user", new { email = o.User.Email, displayName = o.User.DisplayName })
                .With("gameProduct", new { name = o.GameProduct.Name })),
            total,
            page,
            limit,
        };
    }

    /// <summary>Sets an order's final status and settles the wallet lock to match, in one transaction.</summary>
    public async Task<Dictionary<string, object?>> OverrideOrderStatusAsync(string id, OverrideStatusDto dto, CancellationToken ct)
    {
        var status = dto.Status.Str() switch
        {
            "COMPLETED" => OrderStatus.COMPLETED,
            "FAILED" => OrderStatus.FAILED,
            "REFUNDED" => OrderStatus.REFUNDED,
            _ => throw new BadRequestException("Invalid status"),
        };

        return await Tx.RunAsync(db, async () =>
        {
            // Locking read: a concurrent worker or second override cannot settle the same order twice.
            var current = (await db.Database
                    .SqlQuery<string>($"SELECT status AS `Value` FROM orders WHERE id = {id} FOR UPDATE")
                    .ToListAsync(ct))
                .Select(Enum.Parse<OrderStatus>).Cast<OrderStatus?>().FirstOrDefault()
                ?? throw new NotFoundException("Order not found");
            var order = await db.Orders.SingleAsync(o => o.Id == id, ct);
            var walletId = await wallet.WalletIdForUserAsync(order.UserId, ct);
            var wasLocked = current is OrderStatus.PENDING or OrderStatus.PROCESSING;

            if (status == OrderStatus.FAILED && wasLocked)
                await wallet.UnlockAsync(walletId, order.TotalPrice, WalletTransactionType.TOPUP_REFUND,
                    "Order failed — admin override", order.Id, "ORDER", ct);
            else if (status == OrderStatus.COMPLETED && wasLocked)
                await wallet.DebitLockedAsync(walletId, order.TotalPrice, "Order completed — admin override", order.Id, "ORDER", ct);
            else if (status == OrderStatus.REFUNDED && current == OrderStatus.COMPLETED)
                // The funds already left the lock when the order completed: credit them back.
                await wallet.CreditAsync(walletId, order.TotalPrice, WalletTransactionType.TOPUP_REFUND,
                    "Order refunded — admin override", order.Id, "ORDER", ct);
            else if (status == OrderStatus.REFUNDED && wasLocked)
                await wallet.UnlockAsync(walletId, order.TotalPrice, WalletTransactionType.TOPUP_REFUND,
                    "Order refunded — admin override", order.Id, "ORDER", ct);

            order.Status = status;
            await db.SaveChangesAsync(ct);
            return Rows.Of(order);
        }, ct);
    }

    public async Task<object> RetryOrderAsync(string id, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Order not found");
        if (order.Status != OrderStatus.FAILED)
            throw new BadRequestException("Only failed orders can be retried");

        // A failed order was refunded, so its lock is gone: lock the funds again, or a successful retry would deliver
        // the top-up and then fail to debit it. PENDING before queueing, or the worker could still see FAILED.
        await Tx.RunAsync(db, async () =>
        {
            var walletId = await wallet.WalletIdForUserAsync(order.UserId, ct);
            await wallet.LockAsync(walletId, order.TotalPrice, "Wallet lock for order retry", order.Id, "ORDER", ct);
            order.Status = OrderStatus.PENDING;
            order.RetryCount = 0;
            await db.SaveChangesAsync(ct);
        }, ct);
        jobs.EnqueueTopup(order.Id, order.UserId, order.GameProductId);
        return new { message = "Order queued for retry" };
    }

    public async Task<List<Dictionary<string, object?>>> ListProvidersAsync(CancellationToken ct)
    {
        var providers = await db.Providers.AsNoTracking().OrderBy(p => p.Priority).ToListAsync(ct);
        return providers.Select(p => Rows.Pick(p, ProviderColumns)).ToList();
    }

    public async Task<Dictionary<string, object?>> CreateProviderAsync(CreateProviderDto dto, CancellationToken ct)
    {
        var provider = new Provider
        {
            Name = dto.Name.Str()!,
            Slug = dto.Slug.Str()!,
            ApiUrl = dto.ApiUrl.Str()!,
            ApiKeyEnc = secrets.Encrypt(dto.ApiKey.Str()!),
            ApiSecretEnc = dto.ApiSecret.Str() is { Length: > 0 } secret ? secrets.Encrypt(secret) : null,
            HealthCheckUrl = dto.HealthCheckUrl.Str() is { Length: > 0 } url ? url : null,
        };
        dto.Priority.Set(e => e.Int() ?? throw Null("priority"), v => provider.Priority = v);
        dto.IsActive.Set(e => e.Bool() ?? throw Null("isActive"), v => provider.IsActive = v);
        dto.RateLimitPerMin.Set(e => e.Int() ?? throw Null("rateLimitPerMin"), v => provider.RateLimitPerMin = v);
        db.Providers.Add(provider);
        await db.SaveChangesAsync(ct);
        return Rows.Pick(provider, ProviderColumns);
    }

    public async Task<Dictionary<string, object?>> UpdateProviderAsync(string id, UpdateProviderDto dto, CancellationToken ct)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Provider not found");
        dto.Name.Set(e => e.Str() ?? throw Null("name"), v => provider.Name = v);
        dto.ApiUrl.Set(e => e.Str() ?? throw Null("apiUrl"), v => provider.ApiUrl = v);
        dto.Priority.Set(e => e.Int() ?? throw Null("priority"), v => provider.Priority = v);
        dto.IsActive.Set(e => e.Bool() ?? throw Null("isActive"), v => provider.IsActive = v);
        dto.RateLimitPerMin.Set(e => e.Int() ?? throw Null("rateLimitPerMin"), v => provider.RateLimitPerMin = v);
        dto.HealthCheckUrl.Set(e => e.Str() is { Length: > 0 } url ? url : null, v => provider.HealthCheckUrl = v);
        // Blank credentials keep the stored value: the form only sends them when rotating.
        if (dto.ApiKey.Str() is { Length: > 0 } key)
            provider.ApiKeyEnc = secrets.Encrypt(key);
        if (dto.ApiSecret.Str() is { Length: > 0 } secret)
            provider.ApiSecretEnc = secrets.Encrypt(secret);
        await db.SaveChangesAsync(ct);
        return Rows.Pick(provider, ProviderColumns);
    }

    private static InvalidOperationException Null(string field) => new($"Argument {field} must not be null.");
}

public sealed class AnalyticsService(IAppDbContext db, TimeProvider clock)
{
    private sealed record RevenueRow(string Date, decimal? Revenue, long Count);

    private sealed record StatusRow(string Status, long Count);

    private sealed record GrowthRow(DateTime Date, long Count);

    /// <summary>Completed-order revenue per hour (period=day) or per day (week, month).</summary>
    public async Task<object> GetRevenueTrendAsync(string? period, CancellationToken ct)
    {
        var since = Since(period);
        var rows = period == "day"
            ? await db.Database.SqlQuery<RevenueRow>($"""
                SELECT DATE_FORMAT(created_at, '%Y-%m-%d %H:00:00') AS Date, SUM(total_price) AS Revenue, COUNT(*) AS Count
                FROM orders WHERE status = 'COMPLETED' AND created_at >= {since}
                GROUP BY DATE_FORMAT(created_at, '%Y-%m-%d %H:00:00') ORDER BY Date ASC
                """).ToListAsync(ct)
            : await db.Database.SqlQuery<RevenueRow>($"""
                SELECT DATE_FORMAT(created_at, '%Y-%m-%d') AS Date, SUM(total_price) AS Revenue, COUNT(*) AS Count
                FROM orders WHERE status = 'COMPLETED' AND created_at >= {since}
                GROUP BY DATE_FORMAT(created_at, '%Y-%m-%d') ORDER BY Date ASC
                """).ToListAsync(ct);

        // Node returned DATE() as a JS Date (midnight UTC) and the hourly bucket as a plain string.
        return rows.Select(r => new
        {
            date = period == "day" ? (object)r.Date : DateTime.SpecifyKind(DateTime.Parse(r.Date,
                System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc),
            revenue = (double)(r.Revenue ?? 0),
            count = r.Count,
        });
    }

    public async Task<Dictionary<string, long>> GetOrderStatsAsync(string? period, CancellationToken ct)
    {
        var since = Since(period);
        var rows = await db.Database.SqlQuery<StatusRow>(
                $"SELECT status AS Status, COUNT(*) AS Count FROM orders WHERE created_at >= {since} GROUP BY status")
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Status, r => r.Count);
    }

    public async Task<object> GetUserGrowthAsync(CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<GrowthRow>($"""
            SELECT DATE(created_at) AS Date, COUNT(*) AS Count FROM users
            WHERE created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
            GROUP BY DATE(created_at) ORDER BY Date ASC
            """).ToListAsync(ct);
        return rows.Select(r => new { date = DateTime.SpecifyKind(r.Date, DateTimeKind.Utc), count = r.Count });
    }

    private DateTime Since(string? period)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return period switch
        {
            "day" => now.AddDays(-1),
            "week" => now.AddDays(-7),
            _ => now.AddMonths(-1),
        };
    }
}

public sealed record AuditEntry(string? UserId, string Action, string? NewValuesJson, string? IpAddress, string? UserAgent);

public sealed class AuditService(IAppDbContext db)
{
    public async Task LogAsync(AuditEntry entry, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = entry.UserId,
            Action = entry.Action,
            NewValues = entry.NewValuesJson is null ? null : new RawJson(entry.NewValuesJson),
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
        });
        await db.SaveChangesAsync(ct);
    }
}
