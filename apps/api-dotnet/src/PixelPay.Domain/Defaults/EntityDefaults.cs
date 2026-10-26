using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;

namespace PixelPay.Domain.Entities;

// Prisma @default(...) values, applied client-side because the context never lets the DB fill them.
// Hand-written: scaffold.ps1 only overwrites Entities/.
internal static class PrismaDefaults
{
    public static string NewId() => Guid.NewGuid().ToString();
    public static DateTime Now() => DateTime.UtcNow;
    public static RawJson EmptyJson() => new("{}");
}

public partial class Affiliate
{
    public Affiliate()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        CommissionRate = 2.00m;
        Status = AffiliateStatus.ACTIVE;
    }
}

public partial class AffiliateCommission
{
    public AffiliateCommission()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        Status = CommissionStatus.PENDING;
    }
}

public partial class AuditLog
{
    public AuditLog()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
    }
}

public partial class CashbackRule
{
    public CashbackRule()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
    }
}

public partial class Category
{
    public Category()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
    }
}

public partial class Coupon
{
    public Coupon()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
        UserLimit = 1;
    }
}

public partial class CouponUsage
{
    public CouponUsage()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
    }
}

public partial class Game
{
    public Game()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
        Metadata = PrismaDefaults.EmptyJson();
    }
}

public partial class GameProduct
{
    public GameProduct()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
        Metadata = PrismaDefaults.EmptyJson();
        Currency = "THB";
        ProductType = ProductType.DIRECT;
    }
}

public partial class Notification
{
    public Notification()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        Metadata = PrismaDefaults.EmptyJson();
    }
}

public partial class Order
{
    public Order()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        Metadata = PrismaDefaults.EmptyJson();
        Quantity = 1;
        Status = OrderStatus.PENDING;
    }
}

public partial class Payment
{
    public Payment()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        Currency = "THB";
        Status = PaymentStatus.PENDING;
    }
}

public partial class Provider
{
    public Provider()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
        Metadata = PrismaDefaults.EmptyJson();
        Priority = 1;
        RateLimitPerMin = 60;
    }
}

public partial class ProviderProduct
{
    public ProviderProduct()
    {
        Id = PrismaDefaults.NewId();
        IsAvailable = true;
    }
}

public partial class Reseller
{
    public Reseller()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        DiscountRate = 5.00m;
        Status = ResellerStatus.PENDING;
    }
}

public partial class User
{
    public User()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
        IsActive = true;
        Role = UserRole.USER;
    }
}

public partial class Wallet
{
    public Wallet()
    {
        Id = PrismaDefaults.NewId();
        Currency = "THB";
    }
}

public partial class WalletTransaction
{
    public WalletTransaction()
    {
        Id = PrismaDefaults.NewId();
        CreatedAt = PrismaDefaults.Now();
    }
}
