using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PixelPay.Api.Auth;
using PixelPay.Api.Json;
using PixelPay.Application.Admin;
using PixelPay.Application.Catalog;
using PixelPay.Application.Promotions;

namespace PixelPay.Api.Controllers;

[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin")]
[ServiceFilter<AuditFilter>]
public class AdminController(
    AdminService admin, AnalyticsService analytics, ResellersService resellers, GamesService games,
    CategoriesService categories) : NestController
{
    [HttpGet("stats")]
    public Task<object> GetStats(CancellationToken ct) => admin.GetDashboardStatsAsync(ct);

    [HttpGet("users")]
    public Task<object> ListUsers(CancellationToken ct) =>
        admin.ListUsersAsync(Number("page", 1), Number("limit", 20), Text("search"), ct);

    [HttpPatch("users/{id}")]
    public Task<Dictionary<string, object?>> UpdateUser(string id, UpdateUserDto dto, CancellationToken ct) =>
        admin.UpdateUserAsync(id, dto, ct);

    [HttpGet("orders")]
    public Task<object> ListOrders(CancellationToken ct) =>
        admin.ListOrdersAsync(Number("page", 1), Number("limit", 20), Text("status"), Text("userId"), ct);

    [HttpPatch("orders/{id}/status")]
    public Task<Dictionary<string, object?>> OverrideOrderStatus(string id, OverrideStatusDto dto, CancellationToken ct) =>
        admin.OverrideOrderStatusAsync(id, dto, ct);

    [HttpPost("orders/{id}/retry")]
    public Task<object> RetryOrder(string id, CancellationToken ct) => admin.RetryOrderAsync(id, ct);

    [HttpGet("providers")]
    public Task<List<Dictionary<string, object?>>> ListProviders(CancellationToken ct) => admin.ListProvidersAsync(ct);

    [HttpPost("providers")]
    public Task<Dictionary<string, object?>> CreateProvider(CreateProviderDto dto, CancellationToken ct) =>
        admin.CreateProviderAsync(dto, ct);

    [HttpPatch("providers/{id}")]
    public Task<Dictionary<string, object?>> UpdateProvider(string id, UpdateProviderDto dto, CancellationToken ct) =>
        admin.UpdateProviderAsync(id, dto, ct);

    [HttpGet("analytics/revenue")]
    public Task<object> GetRevenue(CancellationToken ct) => analytics.GetRevenueTrendAsync(Text("period") ?? "month", ct);

    [HttpGet("analytics/orders")]
    public Task<Dictionary<string, long>> GetOrderStats(CancellationToken ct) =>
        analytics.GetOrderStatsAsync(Text("period") ?? "month", ct);

    [HttpGet("analytics/users")]
    public Task<object> GetUserGrowth(CancellationToken ct) => analytics.GetUserGrowthAsync(ct);

    [HttpGet("resellers")]
    public Task<List<Dictionary<string, object?>>> ListResellers(CancellationToken ct) =>
        resellers.ListAsync(Number("page", 1), 20, ct);

    [HttpPatch("resellers/{id}/approve")]
    public Task<Dictionary<string, object?>> ApproveReseller(string id, CancellationToken ct) =>
        resellers.ApproveAsync(id, UserId, ct);

    [HttpPatch("resellers/{id}/suspend")]
    public Task<Dictionary<string, object?>> SuspendReseller(string id, CancellationToken ct) => resellers.SuspendAsync(id, ct);

    // ── games ────────────────────────────────────────────────────────────────

    [HttpGet("games")]
    public Task<List<Dictionary<string, object?>>> ListGames(CancellationToken ct) => games.AdminListGamesAsync(ct);

    [HttpPost("games")]
    public Task<Dictionary<string, object?>> CreateGame(CreateGameDto dto, CancellationToken ct) =>
        games.AdminCreateGameAsync(dto.Fields, ct);

    [HttpPatch("games/{id}")]
    public Task<Dictionary<string, object?>> UpdateGame(string id, UpdateGameDto dto, CancellationToken ct) =>
        games.AdminUpdateGameAsync(id, dto.Fields, ct);

    [HttpDelete("games/{id}")]
    public async Task<IActionResult> DeleteGame(string id, CancellationToken ct)
    {
        await games.AdminDeleteGameAsync(id, ct);
        return NoContent();
    }

    // ── products ─────────────────────────────────────────────────────────────

    [HttpGet("games/{id}/products")]
    public Task<List<Dictionary<string, object?>>> ListProducts(string id, CancellationToken ct) => games.AdminListProductsAsync(id, ct);

    [HttpPost("games/{id}/products")]
    public Task<Dictionary<string, object?>> CreateProduct(string id, CreateProductDto dto, CancellationToken ct) =>
        games.AdminCreateProductAsync(id, dto.Fields, ct);

    [HttpPatch("games/{id}/products/{productId}")]
    public Task<Dictionary<string, object?>> UpdateProduct(string productId, UpdateProductDto dto, CancellationToken ct) =>
        games.AdminUpdateProductAsync(productId, dto.Fields, ct);

    [HttpDelete("games/{id}/products/{productId}")]
    public async Task<IActionResult> DeleteProduct(string productId, CancellationToken ct)
    {
        await games.AdminDeleteProductAsync(productId, ct);
        return NoContent();
    }

    // ── categories ───────────────────────────────────────────────────────────

    [HttpGet("categories")]
    public Task<List<Dictionary<string, object?>>> ListCategories(CancellationToken ct) => categories.AdminListAsync(ct);

    [HttpPost("categories")]
    public Task<Dictionary<string, object?>> CreateCategory(CreateCategoryDto dto, CancellationToken ct) =>
        categories.AdminCreateAsync(dto.Fields, ct);

    [HttpPatch("categories/{id}")]
    public Task<Dictionary<string, object?>> UpdateCategory(string id, UpdateCategoryDto dto, CancellationToken ct) =>
        categories.AdminUpdateAsync(id, dto.Fields, ct);

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(string id, CancellationToken ct)
    {
        await categories.AdminDeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>
/// Node's AuditInterceptor: after a successful admin call, store <c>METHOD:AdminController:handler</c> with the
/// response. Audit failures never break the response.
/// </summary>
public sealed class AuditFilter(AuditService audit, ILogger<AuditFilter> log) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
            return;

        var http = context.HttpContext;
        var handler = context.ActionDescriptor is ControllerActionDescriptor d ? d.ActionName : "unknown";
        var value = executed.Result is ObjectResult o ? o.Value : null;
        var newValues = value is null or string or ValueType
            ? JsonSerializer.Serialize(new { value }, ApiJson.Options)
            : JsonSerializer.Serialize(value, value.GetType(), ApiJson.Options);
        try
        {
            await audit.LogAsync(new AuditEntry(
                http.User.FindFirst("sub")?.Value,
                $"{http.Request.Method}:AdminController:{char.ToLowerInvariant(handler[0])}{handler[1..]}",
                newValues,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString()), CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Audit log failed for {Action}", handler);
        }
    }
}
