using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Order
{
    public string Id { get; set; } = null!;

    public string OrderNumber { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string GameProductId { get; set; } = null!;

    public string? ProviderId { get; set; }

    public string? CouponId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal CashbackAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string GameUid { get; set; } = null!;

    public string? GameServer { get; set; }

    public string? GameUsername { get; set; }

    public OrderStatus Status { get; set; }

    public string? ProviderOrderId { get; set; }

    public string? ProviderReference { get; set; }

    public int RetryCount { get; set; }

    public RawJson? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<AffiliateCommission> AffiliateCommissions { get; set; } = new List<AffiliateCommission>();

    public virtual Coupon? Coupon { get; set; }

    public virtual CouponUsage? CouponUsage { get; set; }

    public virtual GameProduct GameProduct { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Provider? Provider { get; set; }

    public virtual User User { get; set; } = null!;
}
