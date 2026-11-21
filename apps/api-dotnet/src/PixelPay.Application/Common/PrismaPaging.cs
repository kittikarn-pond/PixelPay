using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace PixelPay.Application.Common;

/// <summary>Prisma's skip/take rules for the (page - 1) * limit pattern, JS NaN and fractions included.</summary>
public static class PrismaPaging
{
    /// <summary>Newest first. Prisma rejects a non-Int or negative skip and a non-Int take; Nest turns that into a 500.</summary>
    public static async Task<List<T>> NewestFirstAsync<T>(
        IQueryable<T> query, Expression<Func<T, DateTime>> createdAt, double page, double limit, CancellationToken ct)
    {
        var skip = PrismaInt((page - 1) * limit, "skip");
        var take = PrismaInt(limit, "take");
        if (skip < 0)
            throw new InvalidOperationException($"skip must be positive, found {skip}");

        // A negative take counts from the end of the list, as in Prisma.
        return take >= 0
            ? await query.OrderByDescending(createdAt).Skip(skip).Take(take).ToListAsync(ct)
            : (await query.OrderBy(createdAt).Skip(skip).Take(-take).ToListAsync(ct)).AsEnumerable().Reverse().ToList();
    }

    private static int PrismaInt(double value, string argument) =>
        double.IsInteger(value) && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new InvalidOperationException($"Invalid value for {argument}: {value}");
}
