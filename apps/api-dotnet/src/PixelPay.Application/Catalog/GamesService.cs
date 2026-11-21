using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Catalog;

public sealed class GamesService(IAppDbContext db)
{
    // ── public storefront ────────────────────────────────────────────────────

    public async Task<List<Dictionary<string, object?>>> FindAllAsync(CancellationToken ct)
    {
        var games = await db.Games.AsNoTracking().Include(g => g.Category)
            .Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToListAsync(ct);
        return games.Select(g => Rows.Pick(g, "id", "name", "slug", "logoUrl", "sortOrder")
            .With("category", CategoryBrief(g.Category))).ToList();
    }

    public async Task<Dictionary<string, object?>> FindBySlugAsync(string slug, CancellationToken ct)
    {
        var g = await db.Games.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Slug == slug, ct)
            ?? throw new NotFoundException("Game not found");
        return Rows.Pick(g, "id", "name", "slug", "logoUrl", "bannerUrl", "description", "descriptionTh")
            .With("category", CategoryBrief(g.Category));
    }

    public async Task<List<Dictionary<string, object?>>> FindProductsAsync(string slug, CancellationToken ct)
    {
        var gameId = (string)(await FindBySlugAsync(slug, ct))["id"]!;
        var products = await db.GameProducts.AsNoTracking()
            .Where(p => p.GameId == gameId && p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(ct);
        return products.Select(p => Rows.Of(p)).ToList();
    }

    // ── admin: games ─────────────────────────────────────────────────────────

    public async Task<List<Dictionary<string, object?>>> AdminListGamesAsync(CancellationToken ct)
    {
        var games = await db.Games.AsNoTracking().Include(g => g.Category).OrderBy(g => g.SortOrder).ToListAsync(ct);
        return games.Select(g => Rows.Of(g).With("category", CategoryBrief(g.Category))).ToList();
    }

    public async Task<Dictionary<string, object?>> AdminCreateGameAsync(GameDto dto, CancellationToken ct)
    {
        var slug = dto.Slug.Str()!;
        if (await db.Games.AnyAsync(g => g.Slug == slug, ct))
            throw new ConflictException($"Slug \"{slug}\" is already in use");

        var game = new Game();
        Apply(game, dto);
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);
        return Rows.Of(game);
    }

    public async Task<Dictionary<string, object?>> AdminUpdateGameAsync(string id, GameDto dto, CancellationToken ct)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Game not found");
        if (dto.Slug.Str() is { Length: > 0 } slug && await db.Games.AnyAsync(g => g.Slug == slug && g.Id != id, ct))
            throw new ConflictException($"Slug \"{slug}\" is already in use");

        Apply(game, dto);
        await db.SaveChangesAsync(ct);
        return Rows.Of(game);
    }

    public async Task AdminDeleteGameAsync(string id, CancellationToken ct)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Game not found");
        if (await db.Orders.AnyAsync(o => o.GameProduct.GameId == id, ct))
            throw new ConflictException("Cannot delete game: it has products with order history. Set isActive to false instead.");

        await db.GameProducts.Where(p => p.GameId == id).ExecuteDeleteAsync(ct);
        db.Games.Remove(game);
        await db.SaveChangesAsync(ct);
    }

    // ── admin: products ──────────────────────────────────────────────────────

    public async Task<List<Dictionary<string, object?>>> AdminListProductsAsync(string gameId, CancellationToken ct)
    {
        var products = await db.GameProducts.AsNoTracking()
            .Where(p => p.GameId == gameId).OrderBy(p => p.SortOrder).ToListAsync(ct);
        return products.Select(p => Rows.Of(p)).ToList();
    }

    public async Task<Dictionary<string, object?>> AdminCreateProductAsync(string gameId, ProductDto dto, CancellationToken ct)
    {
        if (!await db.Games.AnyAsync(g => g.Id == gameId, ct))
            throw new NotFoundException("Game not found");
        var sku = dto.Sku.Str()!;
        if (await db.GameProducts.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"SKU \"{sku}\" is already in use");

        var product = new GameProduct { GameId = gameId };
        Apply(product, dto);
        db.GameProducts.Add(product);
        await db.SaveChangesAsync(ct);
        return Rows.Of(product);
    }

    public async Task<Dictionary<string, object?>> AdminUpdateProductAsync(string id, ProductDto dto, CancellationToken ct)
    {
        var product = await db.GameProducts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product not found");
        if (dto.Sku.Str() is { Length: > 0 } sku && await db.GameProducts.AnyAsync(p => p.Sku == sku && p.Id != id, ct))
            throw new ConflictException($"SKU \"{sku}\" is already in use");

        Apply(product, dto);
        await db.SaveChangesAsync(ct);
        return Rows.Of(product);
    }

    public async Task AdminDeleteProductAsync(string id, CancellationToken ct)
    {
        var product = await db.GameProducts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product not found");
        if (await db.Orders.AnyAsync(o => o.GameProductId == id, ct))
            throw new ConflictException("Cannot delete product: it has order history. Set isActive to false instead.");

        db.GameProducts.Remove(product);
        await db.SaveChangesAsync(ct);
    }

    internal static Dictionary<string, object?>? CategoryBrief(Category? c) =>
        c is null ? null : Rows.Pick(c, "id", "name", "slug");

    private static void Apply(Game g, GameDto d)
    {
        d.Name.Set(e => e.GetString()!, v => g.Name = v);
        d.Slug.Set(e => e.GetString()!, v => g.Slug = v);
        d.LogoUrl.Set(e => e.Str(), v => g.LogoUrl = v);
        d.BannerUrl.Set(e => e.Str(), v => g.BannerUrl = v);
        d.Description.Set(e => e.Str(), v => g.Description = v);
        d.DescriptionTh.Set(e => e.Str(), v => g.DescriptionTh = v);
        d.CategoryId.Set(e => e.Str(), v => g.CategoryId = v);
        d.IsActive.Set(e => e.Bool() ?? throw Null("isActive"), v => g.IsActive = v);
        d.SortOrder.Set(e => e.Int() ?? throw Null("sortOrder"), v => g.SortOrder = v);
    }

    private static void Apply(GameProduct p, ProductDto d)
    {
        d.Name.Set(e => e.GetString()!, v => p.Name = v);
        d.Sku.Set(e => e.GetString()!, v => p.Sku = v);
        d.PriceCost.Set(e => e.Dec() ?? throw Null("priceCost"), v => p.PriceCost = v);
        d.PriceSell.Set(e => e.Dec() ?? throw Null("priceSell"), v => p.PriceSell = v);
        d.Currency.Set(e => e.Str() ?? throw Null("currency"), v => p.Currency = v);
        d.ProductType.Set(e => Enum.Parse<ProductType>(e.Str() ?? throw Null("productType")), v => p.ProductType = v);
        d.RequiresServer.Set(e => e.Bool() ?? throw Null("requiresServer"), v => p.RequiresServer = v);
        d.RequiresUsername.Set(e => e.Bool() ?? throw Null("requiresUsername"), v => p.RequiresUsername = v);
        d.IsActive.Set(e => e.Bool() ?? throw Null("isActive"), v => p.IsActive = v);
        d.SortOrder.Set(e => e.Int() ?? throw Null("sortOrder"), v => p.SortOrder = v);
    }

    // Prisma rejects NULL for a required column; Nest surfaces that as a 500.
    internal static InvalidOperationException Null(string field) => new($"Argument {field} must not be null.");
}

public sealed class CategoriesService(IAppDbContext db)
{
    public async Task<List<Dictionary<string, object?>>> FindAllActiveAsync(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct);
        return categories.Select(c => Rows.Pick(c, "id", "name", "slug")).ToList();
    }

    public async Task<List<Dictionary<string, object?>>> AdminListAsync(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(ct);
        return categories.Select(c => Rows.Of(c)).ToList();
    }

    public async Task<Dictionary<string, object?>> AdminCreateAsync(CategoryDto dto, CancellationToken ct)
    {
        var slug = dto.Slug.Str()!;
        if (await db.Categories.AnyAsync(c => c.Slug == slug, ct))
            throw new ConflictException($"Slug \"{slug}\" is already in use");

        var category = new Category();
        Apply(category, dto);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Rows.Of(category);
    }

    public async Task<Dictionary<string, object?>> AdminUpdateAsync(string id, CategoryDto dto, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found");
        if (dto.Slug.Str() is { Length: > 0 } slug && await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, ct))
            throw new ConflictException($"Slug \"{slug}\" is already in use");

        Apply(category, dto);
        await db.SaveChangesAsync(ct);
        return Rows.Of(category);
    }

    public async Task AdminDeleteAsync(string id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found");
        // games.category_id is ON DELETE SET NULL in the schema, so games simply lose their category.
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Category c, CategoryDto d)
    {
        d.Name.Set(e => e.GetString()!, v => c.Name = v);
        d.Slug.Set(e => e.GetString()!, v => c.Slug = v);
        d.IsActive.Set(e => e.Bool() ?? throw GamesService.Null("isActive"), v => c.IsActive = v);
        d.SortOrder.Set(e => e.Int() ?? throw GamesService.Null("sortOrder"), v => c.SortOrder = v);
    }
}

