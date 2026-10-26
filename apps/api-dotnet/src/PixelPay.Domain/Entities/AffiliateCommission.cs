using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class AffiliateCommission
{
    public string Id { get; set; } = null!;

    public string AffiliateId { get; set; } = null!;

    public string ReferredUserId { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public decimal CommissionAmount { get; set; }

    public CommissionStatus Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Affiliate Affiliate { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual User ReferredUser { get; set; } = null!;
}
