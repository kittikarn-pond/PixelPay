using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class CashbackRule
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public CashbackType CashbackType { get; set; }

    public decimal Value { get; set; }

    public decimal MinOrderAmount { get; set; }

    public string? GameId { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Game? Game { get; set; }
}
