using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Coupon
{
    public string Id { get; set; } = null!;

    public string Code { get; set; } = null!;

    public DiscountType DiscountType { get; set; }

    public decimal Value { get; set; }

    public decimal MinOrderAmount { get; set; }

    public decimal? MaxDiscount { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public int UserLimit { get; set; }

    public string? GameId { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual Game? Game { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
