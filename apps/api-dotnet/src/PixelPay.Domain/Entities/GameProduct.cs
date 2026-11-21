using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class GameProduct
{
    public string Id { get; set; } = null!;

    public string GameId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Sku { get; set; } = null!;

    public decimal PriceCost { get; set; }

    public decimal PriceSell { get; set; }

    public string Currency { get; set; } = null!;

    public ProductType ProductType { get; set; }

    public bool RequiresServer { get; set; }

    public bool RequiresUsername { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public RawJson? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProviderProduct> ProviderProducts { get; set; } = new List<ProviderProduct>();
}
