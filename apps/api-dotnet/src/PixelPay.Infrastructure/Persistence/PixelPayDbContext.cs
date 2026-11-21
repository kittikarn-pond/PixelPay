using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PixelPay.Domain.Entities;

namespace PixelPay.Infrastructure.Persistence;

public partial class PixelPayDbContext : DbContext
{
    public PixelPayDbContext(DbContextOptions<PixelPayDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Affiliate> Affiliates { get; set; }

    public virtual DbSet<AffiliateCommission> AffiliateCommissions { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CashbackRule> CashbackRules { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<CouponUsage> CouponUsages { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<GameProduct> GameProducts { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Provider> Providers { get; set; }

    public virtual DbSet<ProviderProduct> ProviderProducts { get; set; }

    public virtual DbSet<Reseller> Resellers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Affiliate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("affiliates")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ReferralCode, "affiliates_referral_code_key").IsUnique();

            entity.HasIndex(e => e.UserId, "affiliates_user_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CommissionRate)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'2.00'")
                .HasColumnName("commission_rate");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.PendingEarnings)
                .HasPrecision(15, 2)
                .HasColumnName("pending_earnings");
            entity.Property(e => e.ReferralCode)
                .HasMaxLength(20)
                .HasColumnName("referral_code");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'ACTIVE'")
                .HasColumnType("enum('ACTIVE','SUSPENDED')")
                .HasColumnName("status");
            entity.Property(e => e.TotalEarnings)
                .HasPrecision(15, 2)
                .HasColumnName("total_earnings");
            entity.Property(e => e.TotalReferrals).HasColumnName("total_referrals");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.Affiliate)
                .HasForeignKey<Affiliate>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("affiliates_user_id_fkey");
        });

        modelBuilder.Entity<AffiliateCommission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("affiliate_commissions")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.AffiliateId, "affiliate_commissions_affiliate_id_fkey");

            entity.HasIndex(e => e.OrderId, "affiliate_commissions_order_id_fkey");

            entity.HasIndex(e => e.ReferredUserId, "affiliate_commissions_referred_user_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.AffiliateId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("affiliate_id");
            entity.Property(e => e.CommissionAmount)
                .HasPrecision(15, 2)
                .HasColumnName("commission_amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.OrderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("order_id");
            entity.Property(e => e.PaidAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("paid_at");
            entity.Property(e => e.ReferredUserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("referred_user_id");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnType("enum('PENDING','PAID')")
                .HasColumnName("status");

            entity.HasOne(d => d.Affiliate).WithMany(p => p.AffiliateCommissions)
                .HasForeignKey(d => d.AffiliateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("affiliate_commissions_affiliate_id_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.AffiliateCommissions)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("affiliate_commissions_order_id_fkey");

            entity.HasOne(d => d.ReferredUser).WithMany(p => p.AffiliateCommissions)
                .HasForeignKey(d => d.ReferredUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("affiliate_commissions_referred_user_id_fkey");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("audit_logs")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedAt, "idx_audit_created_at").IsDescending();

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "idx_audit_entity");

            entity.HasIndex(e => e.UserId, "idx_audit_user_id");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.EntityId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .HasColumnName("entity_type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(191)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValues)
                .HasColumnType("json")
                .HasColumnName("new_values");
            entity.Property(e => e.OldValues)
                .HasColumnType("json")
                .HasColumnName("old_values");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(191)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<CashbackRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("cashback_rules")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.GameId, "cashback_rules_game_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CashbackType)
                .HasColumnType("enum('PERCENTAGE','FIXED')")
                .HasColumnName("cashback_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("expires_at");
            entity.Property(e => e.GameId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("game_id");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.MinOrderAmount)
                .HasPrecision(15, 2)
                .HasColumnName("min_order_amount");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Value)
                .HasPrecision(15, 2)
                .HasColumnName("value");

            entity.HasOne(d => d.Game).WithMany(p => p.CashbackRules)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("cashback_rules_game_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("categories")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Slug, "categories_slug_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Slug)
                .HasMaxLength(50)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("coupons")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Code, "coupons_code_key").IsUnique();

            entity.HasIndex(e => e.GameId, "coupons_game_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountType)
                .HasColumnType("enum('PERCENTAGE','FIXED')")
                .HasColumnName("discount_type");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("expires_at");
            entity.Property(e => e.GameId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("game_id");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.MaxDiscount)
                .HasPrecision(15, 2)
                .HasColumnName("max_discount");
            entity.Property(e => e.MinOrderAmount)
                .HasPrecision(15, 2)
                .HasColumnName("min_order_amount");
            entity.Property(e => e.UsageLimit).HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount).HasColumnName("used_count");
            entity.Property(e => e.UserLimit)
                .HasDefaultValueSql("'1'")
                .HasColumnName("user_limit");
            entity.Property(e => e.Value)
                .HasPrecision(15, 2)
                .HasColumnName("value");

            entity.HasOne(d => d.Game).WithMany(p => p.Coupons)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("coupons_game_id_fkey");
        });

        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("coupon_usages")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CouponId, "coupon_usages_coupon_id_fkey");

            entity.HasIndex(e => e.OrderId, "coupon_usages_order_id_key").IsUnique();

            entity.HasIndex(e => e.UserId, "coupon_usages_user_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CouponId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("coupon_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(15, 2)
                .HasColumnName("discount_amount");
            entity.Property(e => e.OrderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("order_id");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.Coupon).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("coupon_usages_coupon_id_fkey");

            entity.HasOne(d => d.Order).WithOne(p => p.CouponUsage)
                .HasForeignKey<CouponUsage>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("coupon_usages_order_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("coupon_usages_user_id_fkey");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("games")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Slug, "games_slug_key").IsUnique();

            entity.HasIndex(e => e.CategoryId, "idx_games_category_id");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.BannerUrl)
                .HasMaxLength(191)
                .HasColumnName("banner_url");
            entity.Property(e => e.CategoryId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(191)
                .HasColumnName("description");
            entity.Property(e => e.DescriptionTh)
                .HasMaxLength(191)
                .HasColumnName("description_th");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(191)
                .HasColumnName("logo_url");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.Games)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("games_category_id_fkey");
        });

        modelBuilder.Entity<GameProduct>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("game_products")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Sku, "game_products_sku_key").IsUnique();

            entity.HasIndex(e => e.GameId, "idx_gp_game_id");

            entity.HasIndex(e => e.IsActive, "idx_gp_is_active");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'THB'")
                .HasColumnName("currency");
            entity.Property(e => e.GameId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("game_id");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.PriceCost)
                .HasPrecision(15, 4)
                .HasColumnName("price_cost");
            entity.Property(e => e.PriceSell)
                .HasPrecision(15, 2)
                .HasColumnName("price_sell");
            entity.Property(e => e.ProductType)
                .HasDefaultValueSql("'DIRECT'")
                .HasColumnType("enum('DIRECT','VOUCHER')")
                .HasColumnName("product_type");
            entity.Property(e => e.RequiresServer).HasColumnName("requires_server");
            entity.Property(e => e.RequiresUsername).HasColumnName("requires_username");
            entity.Property(e => e.Sku)
                .HasMaxLength(100)
                .HasColumnName("sku");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Game).WithMany(p => p.GameProducts)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_products_game_id_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("notifications")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "idx_notif_user_id");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Body)
                .HasMaxLength(191)
                .HasColumnName("body");
            entity.Property(e => e.Channel)
                .HasColumnType("enum('EMAIL','SMS','PUSH','IN_APP')")
                .HasColumnName("channel");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.SentAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("sent_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(100)
                .HasColumnName("type");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("orders")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedAt, "idx_orders_created_at").IsDescending();

            entity.HasIndex(e => e.OrderNumber, "idx_orders_order_number").IsUnique();

            entity.HasIndex(e => e.Status, "idx_orders_status");

            entity.HasIndex(e => e.UserId, "idx_orders_user_id");

            entity.HasIndex(e => e.CouponId, "orders_coupon_id_fkey");

            entity.HasIndex(e => e.GameProductId, "orders_game_product_id_fkey");

            entity.HasIndex(e => e.ProviderId, "orders_provider_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.CashbackAmount)
                .HasPrecision(15, 2)
                .HasColumnName("cashback_amount");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("completed_at");
            entity.Property(e => e.CouponId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("coupon_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(15, 2)
                .HasColumnName("discount_amount");
            entity.Property(e => e.GameProductId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("game_product_id");
            entity.Property(e => e.GameServer)
                .HasMaxLength(100)
                .HasColumnName("game_server");
            entity.Property(e => e.GameUid)
                .HasMaxLength(100)
                .HasColumnName("game_uid");
            entity.Property(e => e.GameUsername)
                .HasMaxLength(100)
                .HasColumnName("game_username");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(20)
                .HasColumnName("order_number");
            entity.Property(e => e.PaymentMethod)
                .HasColumnType("enum('WALLET','PROMPTPAY')")
                .HasColumnName("payment_method");
            entity.Property(e => e.ProviderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("provider_id");
            entity.Property(e => e.ProviderOrderId)
                .HasMaxLength(200)
                .HasColumnName("provider_order_id");
            entity.Property(e => e.ProviderReference)
                .HasMaxLength(191)
                .HasColumnName("provider_reference");
            entity.Property(e => e.Quantity)
                .HasDefaultValueSql("'1'")
                .HasColumnName("quantity");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnType("enum('PENDING','PROCESSING','COMPLETED','FAILED','REFUNDED','CANCELLED')")
                .HasColumnName("status");
            entity.Property(e => e.TotalPrice)
                .HasPrecision(15, 2)
                .HasColumnName("total_price");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(15, 2)
                .HasColumnName("unit_price");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.Coupon).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_coupon_id_fkey");

            entity.HasOne(d => d.GameProduct).WithMany(p => p.Orders)
                .HasForeignKey(d => d.GameProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_game_product_id_fkey");

            entity.HasOne(d => d.Provider).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ProviderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_provider_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_user_id_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("payments")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Status, "idx_payments_status");

            entity.HasIndex(e => e.UserId, "idx_payments_user_id");

            entity.HasIndex(e => e.OrderId, "payments_order_id_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(15, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'THB'")
                .HasColumnName("currency");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("expires_at");
            entity.Property(e => e.GatewayReference)
                .HasMaxLength(200)
                .HasColumnName("gateway_reference");
            entity.Property(e => e.OrderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("order_id");
            entity.Property(e => e.PaidAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("paid_at");
            entity.Property(e => e.PaymentMethod)
                .HasColumnType("enum('WALLET','PROMPTPAY')")
                .HasColumnName("payment_method");
            entity.Property(e => e.QrCodeUrl)
                .HasMaxLength(191)
                .HasColumnName("qr_code_url");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnType("enum('PENDING','COMPLETED','FAILED','EXPIRED','REFUNDED')")
                .HasColumnName("status");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payments_order_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_user_id_fkey");
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("providers")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Slug, "providers_slug_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.ApiKeyEnc)
                .HasMaxLength(191)
                .HasColumnName("api_key_enc");
            entity.Property(e => e.ApiSecretEnc)
                .HasMaxLength(191)
                .HasColumnName("api_secret_enc");
            entity.Property(e => e.ApiUrl)
                .HasMaxLength(191)
                .HasColumnName("api_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.HealthCheckUrl)
                .HasMaxLength(191)
                .HasColumnName("health_check_url");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Priority)
                .HasDefaultValueSql("'1'")
                .HasColumnName("priority");
            entity.Property(e => e.RateLimitPerMin)
                .HasDefaultValueSql("'60'")
                .HasColumnName("rate_limit_per_min");
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .HasColumnName("slug");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<ProviderProduct>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("provider_products")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.GameProductId, "provider_products_game_product_id_fkey");

            entity.HasIndex(e => new { e.ProviderId, e.GameProductId }, "provider_products_provider_id_game_product_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.GameProductId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("game_product_id");
            entity.Property(e => e.IsAvailable)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_available");
            entity.Property(e => e.LastChecked)
                .HasColumnType("datetime(3)")
                .HasColumnName("last_checked");
            entity.Property(e => e.ProviderId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("provider_id");
            entity.Property(e => e.ProviderPrice)
                .HasPrecision(15, 4)
                .HasColumnName("provider_price");
            entity.Property(e => e.ProviderSku)
                .HasMaxLength(200)
                .HasColumnName("provider_sku");

            entity.HasOne(d => d.GameProduct).WithMany(p => p.ProviderProducts)
                .HasForeignKey(d => d.GameProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("provider_products_game_product_id_fkey");

            entity.HasOne(d => d.Provider).WithMany(p => p.ProviderProducts)
                .HasForeignKey(d => d.ProviderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("provider_products_provider_id_fkey");
        });

        modelBuilder.Entity<Reseller>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("resellers")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ApprovedBy, "resellers_approved_by_fkey");

            entity.HasIndex(e => e.UserId, "resellers_user_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("approved_by");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(200)
                .HasColumnName("company_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.CreditLimit)
                .HasPrecision(15, 2)
                .HasColumnName("credit_limit");
            entity.Property(e => e.CreditUsed)
                .HasPrecision(15, 2)
                .HasColumnName("credit_used");
            entity.Property(e => e.DiscountRate)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'5.00'")
                .HasColumnName("discount_rate");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnType("enum('PENDING','ACTIVE','SUSPENDED')")
                .HasColumnName("status");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ResellerApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("resellers_approved_by_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.ResellerUser)
                .HasForeignKey<Reseller>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("resellers_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("users")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Phone, "users_phone_key").IsUnique();

            entity.HasIndex(e => e.ReferralCode, "users_referral_code_key").IsUnique();

            entity.HasIndex(e => e.ReferredBy, "users_referred_by_fkey");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(191)
                .HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasColumnName("display_name");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(191)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.ReferralCode)
                .HasMaxLength(12)
                .HasColumnName("referral_code");
            entity.Property(e => e.ReferredBy)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("referred_by");
            entity.Property(e => e.Role)
                .HasDefaultValueSql("'USER'")
                .HasColumnType("enum('USER','ADMIN','RESELLER')")
                .HasColumnName("role");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ReferredByNavigation).WithMany(p => p.InverseReferredByNavigation)
                .HasForeignKey(d => d.ReferredBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("users_referred_by_fkey");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("wallets")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.UserId, "wallets_user_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Balance)
                .HasPrecision(15, 2)
                .HasColumnName("balance");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'THB'")
                .HasColumnName("currency");
            entity.Property(e => e.LockedBalance)
                .HasPrecision(15, 2)
                .HasColumnName("locked_balance");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime(3)")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .HasConstraintName("wallets_user_id_fkey");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("wallet_transactions")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.CreatedAt, "idx_wt_created_at").IsDescending();

            entity.HasIndex(e => e.WalletId, "idx_wt_wallet_id");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(15, 2)
                .HasColumnName("amount");
            entity.Property(e => e.BalanceAfter)
                .HasPrecision(15, 2)
                .HasColumnName("balance_after");
            entity.Property(e => e.BalanceBefore)
                .HasPrecision(15, 2)
                .HasColumnName("balance_before");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(3)")
                .HasColumnType("datetime(3)")
                .HasColumnName("created_at");
            entity.Property(e => e.Metadata)
                .HasColumnType("json")
                .HasColumnName("metadata");
            entity.Property(e => e.ReferenceId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(50)
                .HasColumnName("reference_type");
            entity.Property(e => e.Type)
                .HasColumnType("enum('DEPOSIT','WITHDRAWAL','TOPUP_DEBIT','TOPUP_REFUND','CASHBACK_CREDIT','AFFILIATE_CREDIT','LOCK','UNLOCK')")
                .HasColumnName("type");
            entity.Property(e => e.WalletId)
                .HasMaxLength(36)
                .IsFixedLength()
                .HasColumnName("wallet_id");

            entity.HasOne(d => d.Wallet).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("wallet_transactions_wallet_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
