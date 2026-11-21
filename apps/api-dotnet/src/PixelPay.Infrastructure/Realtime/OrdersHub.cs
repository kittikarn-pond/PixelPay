using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PixelPay.Application.Common;

namespace PixelPay.Infrastructure.Realtime;

/// <summary>
/// Live order status, replacing the Socket.io /orders namespace. Clients connect with their access token
/// (query string <c>access_token</c>) and are put in the group <c>user:{sub}</c>; the server sends
/// <c>order.status</c> with <c>{ orderId, status }</c>.
/// </summary>
[Authorize]
public sealed class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";

    public static string Group(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (userId is null)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(userId));
        await base.OnConnectedAsync();
    }
}

/// <summary>Works from the API and the worker alike: with the Redis backplane, any process can reach any client.</summary>
public sealed class SignalROrderStatusNotifier(IHubContext<OrdersHub> hub) : IOrderStatusNotifier
{
    public Task NotifyAsync(string userId, string orderId, string status, CancellationToken ct = default) =>
        hub.Clients.Group(OrdersHub.Group(userId)).SendAsync("order.status", new { orderId, status }, ct);
}
