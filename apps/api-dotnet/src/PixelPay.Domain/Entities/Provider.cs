using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Provider
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string ApiUrl { get; set; } = null!;

    public string ApiKeyEnc { get; set; } = null!;

    public string? ApiSecretEnc { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public int RateLimitPerMin { get; set; }

    public string? HealthCheckUrl { get; set; }

    public RawJson? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProviderProduct> ProviderProducts { get; set; } = new List<ProviderProduct>();
}
