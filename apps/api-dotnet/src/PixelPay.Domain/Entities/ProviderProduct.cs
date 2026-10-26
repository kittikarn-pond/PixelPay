using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class ProviderProduct
{
    public string Id { get; set; } = null!;

    public string ProviderId { get; set; } = null!;

    public string GameProductId { get; set; } = null!;

    public string ProviderSku { get; set; } = null!;

    public decimal? ProviderPrice { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime? LastChecked { get; set; }

    public virtual GameProduct GameProduct { get; set; } = null!;

    public virtual Provider Provider { get; set; } = null!;
}
