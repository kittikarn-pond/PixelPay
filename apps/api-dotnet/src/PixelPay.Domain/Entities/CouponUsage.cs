using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class CouponUsage
{
    public string Id { get; set; } = null!;

    public string CouponId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public decimal DiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
