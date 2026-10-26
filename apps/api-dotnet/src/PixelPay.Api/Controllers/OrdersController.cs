using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Common;
using PixelPay.Application.Orders;

namespace PixelPay.Api.Controllers;

// Create and cancel stay on Node until Phase 2.
[ApiController]
[Authorize]
[Route("api/v1/orders")]
public class OrdersController(OrdersQueryService orders) : ControllerBase
{
    // Raw query values: Nest's defaults apply only when the key is absent, and parseInt("") is NaN.
    [HttpGet]
    public Task<OrdersPage> List(CancellationToken ct) =>
        orders.FindByUserAsync(HttpContext.GetCurrentUser().Id, Js.ParseInt(Query("page", "1")), Js.ParseInt(Query("limit", "20")), ct);

    [HttpGet("{id}")]
    public Task<OrderDetail> Get(string id, CancellationToken ct) => orders.FindByIdAsync(id, HttpContext.GetCurrentUser().Id, ct);

    private string Query(string key, string fallback) => Request.Query.TryGetValue(key, out var v) ? v.ToString() : fallback;
}
