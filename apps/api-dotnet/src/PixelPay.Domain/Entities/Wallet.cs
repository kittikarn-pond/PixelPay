using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Wallet
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public decimal Balance { get; set; }

    public decimal LockedBalance { get; set; }

    public string Currency { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
