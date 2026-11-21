using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace PixelPay.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations. A database that Prisma created (it has <c>_prisma_migrations</c> but no EF history)
/// is baselined first: InitialCreate is recorded as applied, since its schema is already there.
/// A MySQL named lock keeps two replicas from migrating at once.
/// </summary>
public sealed class DbMigrator(PixelPayDbContext db, ILogger<DbMigrator> log)
{
    private const string BaselineId = "20261121080936_InitialCreate";

    public async Task MigrateAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var locked = await Scalar<long?>("SELECT GET_LOCK('pixelpay_migrate', 120) AS `Value`", ct);
            if (locked != 1)
                throw new InvalidOperationException("Could not take the migration lock within 120 s.");
            try
            {
                await BaselinePrismaDatabaseAsync(ct);
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
                log.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, pending);
                await db.Database.MigrateAsync(ct);
            }
            finally
            {
                await Scalar<long?>("SELECT RELEASE_LOCK('pixelpay_migrate') AS `Value`", ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task BaselinePrismaDatabaseAsync(CancellationToken ct)
    {
        if (!await TableExistsAsync("_prisma_migrations", ct))
            return;

        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        if ((await history.GetAppliedMigrationsAsync(ct)).Any(m => m.MigrationId == BaselineId))
            return;

        var version = typeof(DbContext).Assembly.GetName().Version!;
        await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(BaselineId, $"{version.Major}.{version.Minor}.{version.Build}")), ct);
        log.LogInformation("Baselined a Prisma-created database: {Migration} recorded as applied", BaselineId);
    }

    private async Task<bool> TableExistsAsync(string table, CancellationToken ct) =>
        await Scalar<long>($"SELECT COUNT(*) AS `Value` FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{table}'", ct) > 0;

    private async Task<T> Scalar<T>(string sql, CancellationToken ct) =>
        (await db.Database.SqlQueryRaw<T>(sql).ToListAsync(ct)).Single();
}
