using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;

namespace PixelPay.Application.Orders;

public sealed class OrdersQueryService(IAppDbContext db)
{
    /// <summary><paramref name="page"/> and <paramref name="limit"/> are JS parseInt results, NaN included.</summary>
    public async Task<OrdersPage> FindByUserAsync(string userId, double page, double limit, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var items = await PrismaPaging.NewestFirstAsync(query, o => o.CreatedAt, page, limit, ct);
        var total = await query.CountAsync(ct);
        return new OrdersPage(items.Select(OrderRow.From).ToList(), total, page, limit);
    }

    public async Task<OrderDetail> FindByIdAsync(string id, string userId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.GameProduct).ThenInclude(p => p.Game)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order not found");
        // Node parity: a bare UnauthorizedException, not a 404, for another user's order.
        if (order.UserId != userId)
            throw new UnauthorizedException();
        return OrderDetail.From(order);
    }
}
