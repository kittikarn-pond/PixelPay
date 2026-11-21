using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PixelPay.Domain.Entities;
using PixelPay.Infrastructure.Persistence;

namespace PixelPay.Infrastructure.Seed;

/// <summary>
/// The storefront catalog (ported from prisma/seed.ts; data in Seed/catalog.json). Idempotent: categories and games
/// are upserted by slug, products by SKU, so it can run against a database that already has the catalog.
/// </summary>
public sealed class CatalogSeeder(PixelPayDbContext db, ILogger<CatalogSeeder> log)
{
    private sealed record Catalog(decimal CostRatio, List<SeedCategory> Categories, List<SeedGame> Games);

    private sealed record SeedCategory(string Name, string Slug, int SortOrder);

    private sealed record SeedGame(
        string Name, string Slug, string? CategorySlug, string Description, string DescriptionTh, string LogoUrl,
        int SortOrder, bool RequiresServer, List<SeedProduct> Products);

    private sealed record SeedProduct(string Name, string Sku, decimal PriceSell);

    public async Task SeedAsync(CancellationToken ct)
    {
        var catalog = Load();
        var categoryIds = new Dictionary<string, string>();

        foreach (var c in catalog.Categories)
        {
            var category = await db.Categories.FirstOrDefaultAsync(x => x.Slug == c.Slug, ct) ?? db.Categories.Add(new Category()).Entity;
            category.Name = c.Name;
            category.Slug = c.Slug;
            category.SortOrder = c.SortOrder;
            await db.SaveChangesAsync(ct);
            categoryIds[c.Slug] = category.Id;
        }

        foreach (var g in catalog.Games)
        {
            var game = await db.Games.FirstOrDefaultAsync(x => x.Slug == g.Slug, ct);
            if (game is null)
            {
                game = db.Games.Add(new Game { IsActive = true }).Entity;
            }
            game.Name = g.Name;
            game.Slug = g.Slug;
            game.Description = g.Description;
            game.DescriptionTh = g.DescriptionTh;
            game.LogoUrl = g.LogoUrl;
            game.SortOrder = g.SortOrder;
            game.CategoryId = g.CategorySlug is null ? null : categoryIds[g.CategorySlug];
            await db.SaveChangesAsync(ct);

            for (var i = 0; i < g.Products.Count; i++)
            {
                var p = g.Products[i];
                var product = await db.GameProducts.FirstOrDefaultAsync(x => x.Sku == p.Sku, ct)
                    ?? db.GameProducts.Add(new GameProduct()).Entity;
                product.GameId = game.Id;
                product.Name = p.Name;
                product.Sku = p.Sku;
                product.PriceCost = Math.Round(p.PriceSell * catalog.CostRatio, 2, MidpointRounding.AwayFromZero);
                product.PriceSell = p.PriceSell;
                product.RequiresServer = g.RequiresServer;
                product.SortOrder = i + 1;
                product.IsActive = true;
            }
            await db.SaveChangesAsync(ct);
            log.LogInformation("Seeded {Game} — {Count} products", g.Name, g.Products.Count);
        }
    }

    private static Catalog Load()
    {
        var assembly = typeof(CatalogSeeder).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("catalog.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<Catalog>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
