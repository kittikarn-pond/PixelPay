using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using System;
using System.Collections.Generic;

namespace PixelPay.Domain.Entities;

public partial class Game
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string? BannerUrl { get; set; }

    public string? Description { get; set; }

    public string? DescriptionTh { get; set; }

    public string? CategoryId { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public RawJson? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CashbackRule> CashbackRules { get; set; } = new List<CashbackRule>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();

    public virtual ICollection<GameProduct> GameProducts { get; set; } = new List<GameProduct>();
}
