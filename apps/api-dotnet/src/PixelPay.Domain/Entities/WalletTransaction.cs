using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class WalletTransaction
{
    public string Id { get; set; } = null!;

    public string WalletId { get; set; } = null!;

    public WalletTransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string? ReferenceId { get; set; }

    public string? ReferenceType { get; set; }

    public RawJson? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
