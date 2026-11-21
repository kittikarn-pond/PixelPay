using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PixelPay.Application.Common;
using PixelPay.Application.Wallets;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Promotions;

public sealed record ValidateCouponDto(JsonElement Code, JsonElement OrderTotal, JsonElement GameId);

public sealed class ValidateCouponDtoValidator : DtoValidator<ValidateCouponDto>
{
    public ValidateCouponDtoValidator()
    {
        Field(x => x.Code, "code").String();
        Field(x => x.OrderTotal, "orderTotal").Number().Min(0);
        Field(x => x.GameId, "gameId").Optional().Uuid();
    }
}

/// <summary>discountAmount is a JS number in the response, so it is a double here.</summary>
public sealed record CouponValidation(string CouponId, double DiscountAmount, string Code);

public sealed class CouponsService(IAppDbContext db, TimeProvider clock)
{
    public Task<CouponValidation> ValidateAsync(string userId, ValidateCouponDto dto, CancellationToken ct) =>
        ValidateAsync(userId, dto.Code.Str()!, dto.OrderTotal.Dec() ?? 0, dto.GameId.Str(), ct);

    public async Task<CouponValidation> ValidateAsync(string userId, string code, decimal orderTotal, string? gameId,
        CancellationToken ct)
    {
        var coupon = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct)
            ?? throw new NotFoundException("Coupon not found");
        if (!coupon.IsActive)
            throw new BadRequestException("Coupon is inactive");
        if (coupon.ExpiresAt is { } expires && expires < clock.GetUtcNow().UtcDateTime)
            throw new BadRequestException("Coupon has expired");
        if (coupon.UsageLimit is { } limit && coupon.UsedCount >= limit)
            throw new BadRequestException("Coupon usage limit reached");
        if (orderTotal < coupon.MinOrderAmount)
            throw new BadRequestException($"Minimum order amount is {Money.Js(coupon.MinOrderAmount)} THB");
        if (coupon.GameId is not null && gameId is not null && coupon.GameId != gameId)
            throw new BadRequestException("Coupon is not valid for this game");

        var used = await db.CouponUsages.CountAsync(u => u.CouponId == coupon.Id && u.UserId == userId, ct);
        if (used >= coupon.UserLimit)
            throw new BadRequestException("You have already used this coupon");

        var discount = coupon.DiscountType == DiscountType.PERCENTAGE ? orderTotal * coupon.Value / 100 : coupon.Value;
        if (coupon.MaxDiscount is { } max)
            discount = Math.Min(discount, max);
        // Whole satang: the order and wallet columns are DECIMAL(15,2).
        discount = Money.Round(Math.Min(discount, orderTotal));

        return new CouponValidation(coupon.Id, (double)discount, code);
    }

    /// <summary>Records the usage, incrementing usedCount only while the coupon is still under its usage limit.</summary>
    public Task ApplyToOrderAsync(string couponId, string userId, string orderId, decimal discountAmount, CancellationToken ct) =>
        Tx.RunAsync(db, async () =>
        {
            var updated = await db.Coupons
                .Where(c => c.Id == couponId && (c.UsageLimit == null || c.UsedCount < c.UsageLimit))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1), ct);
            if (updated == 0)
                throw new BadRequestException("Coupon usage limit reached");

            db.CouponUsages.Add(new CouponUsage
            {
                CouponId = couponId,
                UserId = userId,
                OrderId = orderId,
                DiscountAmount = discountAmount,
            });
            await db.SaveChangesAsync(ct);
        }, ct);
}

public sealed class CashbackService(IAppDbContext db, WalletService wallet, TimeProvider clock, ILogger<CashbackService> log)
{
    /// <summary>Credits the best matching cashback rule for a completed order; returns the amount (0 when none).</summary>
    public async Task<decimal> EvaluateAndCreditAsync(string userId, string orderId, decimal orderAmount, string gameId,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var rule = await db.CashbackRules.AsNoTracking()
            .Where(r => r.IsActive
                        && (r.GameId == null || r.GameId == gameId)
                        && (r.ExpiresAt == null || r.ExpiresAt >= now)
                        && r.MinOrderAmount <= orderAmount)
            .OrderByDescending(r => r.Value)
            .FirstOrDefaultAsync(ct);
        if (rule is null)
            return 0;

        var amount = rule.CashbackType == CashbackType.PERCENTAGE
            ? Money.Round(orderAmount * rule.Value / 100)
            : Math.Min(rule.Value, orderAmount);
        if (amount <= 0)
            return 0;

        await Tx.RunAsync(db, async () =>
        {
            var now = Domain.Common.PrismaTime.Now();
            await db.Orders.Where(o => o.Id == orderId).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.CashbackAmount, amount)
                .SetProperty(o => o.UpdatedAt, now), ct);
            var walletId = await wallet.WalletIdForUserAsync(userId, ct);
            await wallet.CreditAsync(walletId, amount, WalletTransactionType.CASHBACK_CREDIT, "Cashback for order",
                orderId, "CASHBACK", ct);
        }, ct);

        log.LogInformation("Cashback {Amount} THB credited to user {UserId} for order {OrderId}", amount, userId, orderId);
        return amount;
    }
}

public sealed class AffiliatesService(IAppDbContext db, ILogger<AffiliatesService> log)
{
    public async Task<Affiliate> GetOrCreateAsync(string userId, CancellationToken ct)
    {
        var existing = await db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (existing is not null)
            return existing;

        var referralCode = await db.Users.Where(u => u.Id == userId).Select(u => u.ReferralCode).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"No User found for id {userId}");
        var affiliate = new Affiliate { UserId = userId, ReferralCode = referralCode };
        db.Affiliates.Add(affiliate);
        await db.SaveChangesAsync(ct);
        return affiliate;
    }

    public async Task<object> GetDashboardAsync(string userId, CancellationToken ct)
    {
        var a = await GetOrCreateAsync(userId, ct);
        return new
        {
            referralCode = a.ReferralCode,
            totalReferrals = a.TotalReferrals,
            totalEarnings = (double)a.TotalEarnings,
            pendingEarnings = (double)a.PendingEarnings,
            status = a.Status,
        };
    }

    public async Task<object> GetCommissionsAsync(string userId, double page, double limit, CancellationToken ct)
    {
        var affiliateId = await db.Affiliates.Where(a => a.UserId == userId).Select(a => a.Id).FirstOrDefaultAsync(ct);
        if (affiliateId is null)
            return new { items = Array.Empty<object>(), total = 0, page, limit };

        var query = db.AffiliateCommissions.AsNoTracking().Include(c => c.Order).Where(c => c.AffiliateId == affiliateId);
        var items = await PrismaPaging.NewestFirstAsync(query, c => c.CreatedAt, page, limit, ct);
        var total = await query.CountAsync(ct);
        return new
        {
            items = items.Select(c => Rows.Of(c)
                .With("commissionAmount", (double)c.CommissionAmount)
                .With("order", new
                {
                    orderNumber = c.Order.OrderNumber,
                    totalPrice = (double)c.Order.TotalPrice,
                    createdAt = c.Order.CreatedAt,
                })),
            total,
            page,
            limit,
        };
    }

    public async Task AwardCommissionAsync(string referrerId, string orderId, decimal orderAmount, CancellationToken ct)
    {
        var affiliate = await db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == referrerId, ct);
        if (affiliate is null || affiliate.Status != AffiliateStatus.ACTIVE)
            return;

        var amount = Money.Round(orderAmount * affiliate.CommissionRate / 100);
        if (amount <= 0)
            return;

        await Tx.RunAsync(db, async () =>
        {
            var buyerId = await db.Orders.Where(o => o.Id == orderId).Select(o => o.UserId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"No Order found for id {orderId}");
            db.AffiliateCommissions.Add(new AffiliateCommission
            {
                AffiliateId = affiliate.Id,
                ReferredUserId = buyerId,
                OrderId = orderId,
                CommissionAmount = amount,
            });
            await db.SaveChangesAsync(ct);
            await db.Affiliates.Where(a => a.Id == affiliate.Id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.PendingEarnings, a => a.PendingEarnings + amount)
                .SetProperty(a => a.TotalEarnings, a => a.TotalEarnings + amount), ct);
        }, ct);

        log.LogInformation("Commission {Amount} THB awarded to affiliate {ReferrerId} for order {OrderId}",
            amount, referrerId, orderId);
    }
}

public sealed record ApplyResellerDto(JsonElement CompanyName);

public sealed class ResellersService(IAppDbContext db, TimeProvider clock)
{
    public async Task<Dictionary<string, object?>> ApplyAsync(string userId, ApplyResellerDto dto, CancellationToken ct)
    {
        if (await db.Resellers.AnyAsync(r => r.UserId == userId, ct))
            throw new BadRequestException("Reseller application already exists");

        // Node had no validation on this body; a missing companyName is a Prisma error (500).
        var reseller = new Reseller
        {
            UserId = userId,
            CompanyName = dto.CompanyName.Str() ?? throw new InvalidOperationException("companyName is required"),
            Status = ResellerStatus.PENDING,
        };
        db.Resellers.Add(reseller);
        await db.SaveChangesAsync(ct);
        return Rows.Of(reseller);
    }

    public async Task<Dictionary<string, object?>> ApproveAsync(string id, string adminId, CancellationToken ct)
    {
        var reseller = await db.Resellers.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Reseller not found");
        if (reseller.Status != ResellerStatus.PENDING)
            throw new BadRequestException("Only pending resellers can be approved");

        return await Tx.RunAsync(db, async () =>
        {
            var now = Domain.Common.PrismaTime.Now();
            await db.Users.Where(u => u.Id == reseller.UserId).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, UserRole.RESELLER)
                .SetProperty(u => u.UpdatedAt, now), ct);
            reseller.Status = ResellerStatus.ACTIVE;
            reseller.ApprovedBy = adminId;
            reseller.ApprovedAt = Domain.Common.PrismaTime.Truncate(clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return Rows.Of(reseller);
        }, ct);
    }

    public async Task<Dictionary<string, object?>> SuspendAsync(string id, CancellationToken ct)
    {
        var reseller = await db.Resellers.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Reseller not found");
        reseller.Status = ResellerStatus.SUSPENDED;
        await db.SaveChangesAsync(ct);
        return Rows.Of(reseller);
    }

    public async Task<List<Dictionary<string, object?>>> GetProductsAsync(string userId, CancellationToken ct)
    {
        var reseller = await db.Resellers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == ResellerStatus.ACTIVE, ct)
            ?? throw new ForbiddenException("Active reseller account required");

        var products = await db.GameProducts.AsNoTracking().Include(p => p.Game).Where(p => p.IsActive).ToListAsync(ct);
        return products.Select(p => Rows.Of(p)
            .With("game", new { name = p.Game.Name, slug = p.Game.Slug })
            .With("resellerPrice", (p.PriceSell * (1 - reseller.DiscountRate / 100)).ToString("F2",
                System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    public async Task<List<Dictionary<string, object?>>> ListAsync(double page, double limit, CancellationToken ct)
    {
        var query = db.Resellers.AsNoTracking().Include(r => r.User);
        var items = await PrismaPaging.NewestFirstAsync(query, r => r.CreatedAt, page, limit, ct);
        return items.Select(r => Rows.Of(r).With("user", new { email = r.User.Email, displayName = r.User.DisplayName })).ToList();
    }
}

public static class Money
{
    /// <summary>Two decimal places, half away from zero (JS Math.round(x * 100) / 100 for positive amounts).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>A decimal as Prisma's Decimal.toString() prints it: no trailing zeros.</summary>
    public static string Js(decimal d) =>
        (d / 1.000000000000000000000000000000000m).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
