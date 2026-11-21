using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Reseller
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public decimal DiscountRate { get; set; }

    public decimal CreditLimit { get; set; }

    public decimal CreditUsed { get; set; }

    public ResellerStatus Status { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
