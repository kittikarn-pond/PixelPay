using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Common;
using PixelPay.Application.Orders;

namespace PixelPay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
public class OrdersController(OrdersQueryService orders) : ControllerBase
{
    // Unary +, like Node: the default applies only when the key is absent, and a repeated key is an array (NaN).
    [HttpGet]
    public Task<OrdersPage> List(CancellationToken ct) =>
        orders.FindByUserAsync(HttpContext.GetCurrentUser().Id, Number("page", 1), Number("limit", 20), ct);

    [HttpGet("{id}")]
    public Task<OrderDetail> Get(string id, CancellationToken ct) => orders.FindByIdAsync(id, HttpContext.GetCurrentUser().Id, ct);

    private double Number(string key, double fallback) =>
        !Request.Query.TryGetValue(key, out var v) ? fallback : v.Count == 1 ? Js.ToNumber(v[0]!) : double.NaN;
}
