using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using PixelPay.Application.Common;
using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;

namespace PixelPay.Infrastructure.Persistence;

// Hand-written half of the scaffolded context. scripts/scaffold.ps1 only overwrites PixelPayDbContext.cs.
public partial class PixelPayDbContext : IAppDbContext
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseMySql(ConnectionString(connectionString), ServerVersion.Parse("8.0-mysql"));

    /// <summary>
    /// The settings EF needs. Pomelo uses a <see cref="MySqlDataSource"/> from DI when one is registered,
    /// so that data source must be built from this string too.
    /// </summary>
    public static string ConnectionString(string connectionString) =>
        new MySqlConnectionStringBuilder(connectionString)
        {
            // char(36) ids are plain strings (Prisma uuid), not Guid.
            GuidFormat = MySqlGuidFormat.None,
            // Mandatory for Pomelo.
            AllowUserVariables = true,
            UseAffectedRows = false,
        }.ConnectionString;

    public static PixelPayDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PixelPayDbContext>();
        Configure(options, connectionString);
        return new PixelPayDbContext(options.Options);
    }

    // Prisma @updatedAt is client-side. ExecuteUpdate bypasses this: set UpdatedAt in the setter list.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampUpdatedAt()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var updatedAt = entry.Metadata.FindProperty("UpdatedAt");
            if (updatedAt?.ClrType == typeof(DateTime)) entry.Property("UpdatedAt").CurrentValue = now;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<RawJson>().HaveConversion<RawJsonColumnConverter>();

        builder.Properties<UserRole>().HaveConversion<string>();
        builder.Properties<OrderStatus>().HaveConversion<string>();
        builder.Properties<PaymentMethod>().HaveConversion<string>();
        builder.Properties<PaymentStatus>().HaveConversion<string>();
        builder.Properties<WalletTransactionType>().HaveConversion<string>();
        builder.Properties<DiscountType>().HaveConversion<string>();
        builder.Properties<CashbackType>().HaveConversion<string>();
        builder.Properties<ProductType>().HaveConversion<string>();
        builder.Properties<AffiliateStatus>().HaveConversion<string>();
        builder.Properties<CommissionStatus>().HaveConversion<string>();
        builder.Properties<ResellerStatus>().HaveConversion<string>();
        builder.Properties<NotificationChannel>().HaveConversion<string>();
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // A CLR default (false, 0, first enum member) must be written, not swapped for the column default.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type.IsValueType && type != typeof(DateTime) && property.ValueGenerated != ValueGenerated.Never)
                modelBuilder.Entity(entity.ClrType).Property(property.Name).ValueGeneratedNever();
        }
    }
}
