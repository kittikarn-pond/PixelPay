using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PixelPay.Domain.Entities;

namespace PixelPay.Application.Common;

public interface IAppDbContext
{
    DbSet<Affiliate> Affiliates { get; }
    DbSet<AffiliateCommission> AffiliateCommissions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<CashbackRule> CashbackRules { get; }
    DbSet<Category> Categories { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponUsage> CouponUsages { get; }
    DbSet<Game> Games { get; }
    DbSet<GameProduct> GameProducts { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Order> Orders { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Provider> Providers { get; }
    DbSet<ProviderProduct> ProviderProducts { get; }
    DbSet<Reseller> Resellers { get; }
    DbSet<User> Users { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
