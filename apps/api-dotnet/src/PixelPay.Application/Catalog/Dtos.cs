using System.Text.Json;
using System.Text.RegularExpressions;
using PixelPay.Application.Common;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Catalog;

// JsonElement fields: Undefined = key absent (Prisma leaves the column alone), Null = explicit null.
public sealed record GameDto(
    JsonElement Name, JsonElement Slug, JsonElement LogoUrl, JsonElement BannerUrl, JsonElement Description,
    JsonElement DescriptionTh, JsonElement CategoryId, JsonElement IsActive, JsonElement SortOrder);

public sealed record CreateGameDto(
    JsonElement Name, JsonElement Slug, JsonElement LogoUrl, JsonElement BannerUrl, JsonElement Description,
    JsonElement DescriptionTh, JsonElement CategoryId, JsonElement IsActive, JsonElement SortOrder)
{
    public GameDto Fields => new(Name, Slug, LogoUrl, BannerUrl, Description, DescriptionTh, CategoryId, IsActive, SortOrder);
}

public sealed record UpdateGameDto(
    JsonElement Name, JsonElement Slug, JsonElement LogoUrl, JsonElement BannerUrl, JsonElement Description,
    JsonElement DescriptionTh, JsonElement CategoryId, JsonElement IsActive, JsonElement SortOrder)
{
    public GameDto Fields => new(Name, Slug, LogoUrl, BannerUrl, Description, DescriptionTh, CategoryId, IsActive, SortOrder);
}

public sealed record ProductDto(
    JsonElement Name, JsonElement Sku, JsonElement PriceCost, JsonElement PriceSell, JsonElement Currency,
    JsonElement ProductType, JsonElement RequiresServer, JsonElement RequiresUsername, JsonElement IsActive,
    JsonElement SortOrder);

public sealed record CreateProductDto(
    JsonElement Name, JsonElement Sku, JsonElement PriceCost, JsonElement PriceSell, JsonElement Currency,
    JsonElement ProductType, JsonElement RequiresServer, JsonElement RequiresUsername, JsonElement IsActive,
    JsonElement SortOrder)
{
    public ProductDto Fields => new(Name, Sku, PriceCost, PriceSell, Currency, ProductType, RequiresServer, RequiresUsername,
        IsActive, SortOrder);
}

public sealed record UpdateProductDto(
    JsonElement Name, JsonElement Sku, JsonElement PriceCost, JsonElement PriceSell, JsonElement Currency,
    JsonElement ProductType, JsonElement RequiresServer, JsonElement RequiresUsername, JsonElement IsActive,
    JsonElement SortOrder)
{
    public ProductDto Fields => new(Name, Sku, PriceCost, PriceSell, Currency, ProductType, RequiresServer, RequiresUsername,
        IsActive, SortOrder);
}

public sealed record CategoryDto(JsonElement Name, JsonElement Slug, JsonElement IsActive, JsonElement SortOrder);

public sealed record CreateCategoryDto(JsonElement Name, JsonElement Slug, JsonElement IsActive, JsonElement SortOrder)
{
    public CategoryDto Fields => new(Name, Slug, IsActive, SortOrder);
}

public sealed record UpdateCategoryDto(JsonElement Name, JsonElement Slug, JsonElement IsActive, JsonElement SortOrder)
{
    public CategoryDto Fields => new(Name, Slug, IsActive, SortOrder);
}

internal static partial class SlugRule
{
    public const string Message = "slug must be lowercase letters, numbers, and hyphens only";

    [GeneratedRegex("^[a-z0-9-]+$")]
    public static partial Regex Pattern();
}

public sealed class CreateGameDtoValidator : DtoValidator<CreateGameDto>
{
    public CreateGameDtoValidator()
    {
        Field(x => x.Name, "name").String().MaxLength(100);
        Field(x => x.Slug, "slug").String().MaxLength(100).Matches(SlugRule.Pattern(), SlugRule.Message);
        Field(x => x.LogoUrl, "logoUrl").Optional().Url();
        Field(x => x.BannerUrl, "bannerUrl").Optional().Url();
        Field(x => x.Description, "description").Optional().String().MaxLength(500);
        Field(x => x.DescriptionTh, "descriptionTh").Optional().String().MaxLength(500);
        Field(x => x.CategoryId, "categoryId").Optional().Uuid();
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}

public sealed class UpdateGameDtoValidator : DtoValidator<UpdateGameDto>
{
    public UpdateGameDtoValidator()
    {
        Field(x => x.Name, "name").Optional().String().MaxLength(100);
        Field(x => x.Slug, "slug").Optional().String().MaxLength(100).Matches(SlugRule.Pattern(), SlugRule.Message);
        Field(x => x.LogoUrl, "logoUrl").Optional().Url();
        Field(x => x.BannerUrl, "bannerUrl").Optional().Url();
        Field(x => x.Description, "description").Optional().String().MaxLength(500);
        Field(x => x.DescriptionTh, "descriptionTh").Optional().String().MaxLength(500);
        Field(x => x.CategoryId, "categoryId").Optional().Uuid();
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}

public sealed class CreateProductDtoValidator : DtoValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        Field(x => x.Name, "name").String().MaxLength(200);
        Field(x => x.Sku, "sku").String().MaxLength(100);
        Field(x => x.PriceCost, "priceCost").Number().Min(0);
        Field(x => x.PriceSell, "priceSell").Number().Min(0);
        Field(x => x.Currency, "currency").Optional().String().MaxLength(3);
        Field(x => x.ProductType, "productType").Optional().Enum<ProductType>();
        Field(x => x.RequiresServer, "requiresServer").Optional().Bool();
        Field(x => x.RequiresUsername, "requiresUsername").Optional().Bool();
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}

public sealed class UpdateProductDtoValidator : DtoValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        Field(x => x.Name, "name").Optional().String().MaxLength(200);
        Field(x => x.Sku, "sku").Optional().String().MaxLength(100);
        Field(x => x.PriceCost, "priceCost").Optional().Number().Min(0);
        Field(x => x.PriceSell, "priceSell").Optional().Number().Min(0);
        Field(x => x.Currency, "currency").Optional().String().MaxLength(3);
        Field(x => x.ProductType, "productType").Optional().Enum<ProductType>();
        Field(x => x.RequiresServer, "requiresServer").Optional().Bool();
        Field(x => x.RequiresUsername, "requiresUsername").Optional().Bool();
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}

public sealed class CreateCategoryDtoValidator : DtoValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        Field(x => x.Name, "name").String().MaxLength(50);
        Field(x => x.Slug, "slug").String().MaxLength(50).Matches(SlugRule.Pattern(), SlugRule.Message);
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}

public sealed class UpdateCategoryDtoValidator : DtoValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator()
    {
        Field(x => x.Name, "name").Optional().String().MaxLength(50);
        Field(x => x.Slug, "slug").Optional().String().MaxLength(50).Matches(SlugRule.Pattern(), SlugRule.Message);
        Field(x => x.IsActive, "isActive").Optional().Bool();
        Field(x => x.SortOrder, "sortOrder").Optional().Int().Min(0);
    }
}
