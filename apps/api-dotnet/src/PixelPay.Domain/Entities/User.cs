using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class User
{
    public string Id { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; }

    public bool IsVerified { get; set; }

    public bool IsActive { get; set; }

    public string ReferralCode { get; set; } = null!;

    public string? ReferredBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Affiliate? Affiliate { get; set; }

    public virtual ICollection<AffiliateCommission> AffiliateCommissions { get; set; } = new List<AffiliateCommission>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual ICollection<User> InverseReferredByNavigation { get; set; } = new List<User>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual User? ReferredByNavigation { get; set; }

    public virtual ICollection<Reseller> ResellerApprovedByNavigations { get; set; } = new List<Reseller>();

    public virtual Reseller? ResellerUser { get; set; }

    public virtual Wallet? Wallet { get; set; }
}
