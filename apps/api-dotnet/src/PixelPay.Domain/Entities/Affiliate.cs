using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Affiliate
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string ReferralCode { get; set; } = null!;

    public decimal CommissionRate { get; set; }

    public int TotalReferrals { get; set; }

    public decimal TotalEarnings { get; set; }

    public decimal PendingEarnings { get; set; }

    public AffiliateStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AffiliateCommission> AffiliateCommissions { get; set; } = new List<AffiliateCommission>();

    public virtual User User { get; set; } = null!;
}
