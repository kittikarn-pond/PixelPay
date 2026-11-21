using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Application.Orders;

namespace PixelPay.Api.Controllers;

[Authorize]
[Route("api/v1/orders")]
public class OrdersController(OrdersQueryService queries, OrdersService orders) : NestController
{
    [HttpPost]
    public Task<Dictionary<string, object?>> Create(CreateOrderDto dto, CancellationToken ct) => orders.CreateAsync(UserId, dto, ct);

    [HttpGet]
    public Task<OrdersPage> List(CancellationToken ct) => queries.FindByUserAsync(UserId, Number("page", 1), Number("limit", 20), ct);

    [HttpGet("{id}")]
    public Task<OrderDetail> Get(string id, CancellationToken ct) => queries.FindByIdAsync(id, UserId, ct);

    [HttpPost("{id}/cancel")]
    public Task<object> Cancel(string id, CancellationToken ct) => orders.CancelAsync(id, UserId, ct);
}
