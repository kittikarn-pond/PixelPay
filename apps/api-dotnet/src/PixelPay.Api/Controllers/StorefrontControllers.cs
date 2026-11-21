using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Application.Catalog;
using PixelPay.Application.Notifications;
using PixelPay.Application.Payments;
using PixelPay.Application.Promotions;

namespace PixelPay.Api.Controllers;

[Route("api/v1/games")]
public class GamesController(GamesService games) : NestController
{
    [HttpGet]
    public Task<List<Dictionary<string, object?>>> FindAll(CancellationToken ct) => games.FindAllAsync(ct);

    [HttpGet("{slug}")]
    public Task<Dictionary<string, object?>> FindOne(string slug, CancellationToken ct) => games.FindBySlugAsync(slug, ct);

    [HttpGet("{slug}/products")]
    public Task<List<Dictionary<string, object?>>> FindProducts(string slug, CancellationToken ct) => games.FindProductsAsync(slug, ct);
}

[Route("api/v1/categories")]
public class CategoriesController(CategoriesService categories) : NestController
{
    [HttpGet]
    public Task<List<Dictionary<string, object?>>> FindAll(CancellationToken ct) => categories.FindAllActiveAsync(ct);
}

[Authorize]
[Route("api/v1/coupons")]
public class CouponsController(CouponsService coupons) : NestController
{
    // @HttpCode(200) in Node.
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(ValidateCouponDto dto, CancellationToken ct) =>
        Ok(await coupons.ValidateAsync(UserId, dto, ct));
}

[Authorize]
[Route("api/v1/affiliates")]
public class AffiliatesController(AffiliatesService affiliates) : NestController
{
    [HttpGet("dashboard")]
    public Task<object> Dashboard(CancellationToken ct) => affiliates.GetDashboardAsync(UserId, ct);

    [HttpGet("commissions")]
    public Task<object> Commissions(CancellationToken ct) =>
        affiliates.GetCommissionsAsync(UserId, Number("page", 1), Number("limit", 20), ct);
}

[Authorize]
[Route("api/v1/reseller")]
public class ResellerController(ResellersService resellers) : NestController
{
    [HttpPost("apply")]
    public Task<Dictionary<string, object?>> Apply(ApplyResellerDto dto, CancellationToken ct) => resellers.ApplyAsync(UserId, dto, ct);

    [HttpGet("products")]
    [Authorize(Roles = "RESELLER")]
    public Task<List<Dictionary<string, object?>>> Products(CancellationToken ct) => resellers.GetProductsAsync(UserId, ct);
}

[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController(NotificationsService notifications) : NestController
{
    [HttpGet]
    public Task<object> FindAll(CancellationToken ct) =>
        notifications.FindByUserAsync(UserId, Number("page", 1), Number("limit", 20), ct);

    [HttpPatch("read-all")]
    public Task<object> MarkAllRead(CancellationToken ct) => notifications.MarkAllReadAsync(UserId, ct);

    [HttpPatch("{id}/read")]
    public Task<object> MarkRead(string id, CancellationToken ct) => notifications.MarkReadAsync(id, UserId, ct);
}

[Route("api/v1/payments")]
public class PaymentsController(PaymentsService payments) : NestController
{
    [Authorize]
    [HttpPost("promptpay")]
    public Task<Dictionary<string, object?>> CreatePromptPay(CreatePromptPayDto dto, CancellationToken ct) =>
        payments.CreatePromptPayAsync(UserId, dto, ct);

    [Authorize]
    [HttpGet("{id}")]
    public Task<Dictionary<string, object?>> Get(string id, CancellationToken ct) => payments.GetAsync(id, UserId, ct);

    // GB Prime Pay's callback is unsigned; the service re-queries the charge status instead of trusting this body.
    [HttpPost("webhook")]
    public async Task<object> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return await payments.HandleWebhookAsync(await reader.ReadToEndAsync(ct), ct);
    }
}
