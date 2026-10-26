using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Payment
{
    public string Id { get; set; } = null!;

    public string? OrderId { get; set; }

    public string UserId { get; set; } = null!;

    public PaymentMethod PaymentMethod { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public PaymentStatus Status { get; set; }

    public string? GatewayReference { get; set; }

    public string? QrCodeUrl { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual User User { get; set; } = null!;
}
