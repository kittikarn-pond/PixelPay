using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Orders;

// Prisma row shapes: every column in schema order, nulls included.
public sealed record OrderRow(
    string Id, string OrderNumber, string UserId, string GameProductId, string? ProviderId, string? CouponId,
    int Quantity, decimal UnitPrice, decimal TotalPrice, decimal DiscountAmount, decimal CashbackAmount,
    PaymentMethod PaymentMethod, string GameUid, string? GameServer, string? GameUsername, OrderStatus Status,
    string? ProviderOrderId, string? ProviderReference, int RetryCount, RawJson? Metadata,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt)
{
    public static OrderRow From(Order o) =>
        new(o.Id, o.OrderNumber, o.UserId, o.GameProductId, o.ProviderId, o.CouponId, o.Quantity, o.UnitPrice, o.TotalPrice,
            o.DiscountAmount, o.CashbackAmount, o.PaymentMethod, o.GameUid, o.GameServer, o.GameUsername, o.Status,
            o.ProviderOrderId, o.ProviderReference, o.RetryCount, o.Metadata, o.CreatedAt, o.UpdatedAt, o.CompletedAt);
}

public sealed record GameRow(
    string Id, string Name, string Slug, string? LogoUrl, string? BannerUrl, string? Description, string? DescriptionTh,
    string? CategoryId, bool IsActive, int SortOrder, RawJson? Metadata, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static GameRow From(Game g) =>
        new(g.Id, g.Name, g.Slug, g.LogoUrl, g.BannerUrl, g.Description, g.DescriptionTh, g.CategoryId, g.IsActive,
            g.SortOrder, g.Metadata, g.CreatedAt, g.UpdatedAt);
}

public sealed record GameProductWithGame(
    string Id, string GameId, string Name, string Sku, decimal PriceCost, decimal PriceSell, string Currency,
    ProductType ProductType, bool RequiresServer, bool RequiresUsername, bool IsActive, int SortOrder, RawJson? Metadata,
    DateTime CreatedAt, DateTime UpdatedAt, GameRow Game)
{
    public static GameProductWithGame From(GameProduct p) =>
        new(p.Id, p.GameId, p.Name, p.Sku, p.PriceCost, p.PriceSell, p.Currency, p.ProductType, p.RequiresServer,
            p.RequiresUsername, p.IsActive, p.SortOrder, p.Metadata, p.CreatedAt, p.UpdatedAt, GameRow.From(p.Game));
}

public sealed record OrderDetail(
    string Id, string OrderNumber, string UserId, string GameProductId, string? ProviderId, string? CouponId,
    int Quantity, decimal UnitPrice, decimal TotalPrice, decimal DiscountAmount, decimal CashbackAmount,
    PaymentMethod PaymentMethod, string GameUid, string? GameServer, string? GameUsername, OrderStatus Status,
    string? ProviderOrderId, string? ProviderReference, int RetryCount, RawJson? Metadata,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt, GameProductWithGame GameProduct)
{
    public static OrderDetail From(Order o) =>
        new(o.Id, o.OrderNumber, o.UserId, o.GameProductId, o.ProviderId, o.CouponId, o.Quantity, o.UnitPrice, o.TotalPrice,
            o.DiscountAmount, o.CashbackAmount, o.PaymentMethod, o.GameUid, o.GameServer, o.GameUsername, o.Status,
            o.ProviderOrderId, o.ProviderReference, o.RetryCount, o.Metadata, o.CreatedAt, o.UpdatedAt, o.CompletedAt,
            GameProductWithGame.From(o.GameProduct));
}

// page and limit are JS numbers.
public sealed record OrdersPage(IReadOnlyList<OrderRow> Items, int Total, double Page, double Limit);
