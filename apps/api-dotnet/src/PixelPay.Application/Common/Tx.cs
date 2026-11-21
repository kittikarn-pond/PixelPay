namespace PixelPay.Application.Common;

/// <summary>Prisma <c>$transaction</c>: joins the caller's transaction if there is one, else opens and commits its own.</summary>
public static class Tx
{
    public static async Task<T> RunAsync<T>(IAppDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            return await work();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await work();
        await tx.CommitAsync(ct);
        return result;
    }

    public static Task RunAsync(IAppDbContext db, Func<Task> work, CancellationToken ct) =>
        RunAsync(db, async () =>
        {
            await work();
            return true;
        }, ct);
}
