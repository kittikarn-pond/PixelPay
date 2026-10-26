using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;

namespace PixelPay.Application.Wallets;

public sealed class WalletService(IAppDbContext db)
{
    public async Task<WalletWithTransactions> GetWalletAsync(string userId, CancellationToken ct)
    {
        var w = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new NotFoundException("Wallet not found");
        var transactions = await db.WalletTransactions.AsNoTracking()
            .Where(t => t.WalletId == w.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        return new WalletWithTransactions(w.Id, w.UserId, w.Balance, w.LockedBalance, w.Currency, w.UpdatedAt,
            transactions.Select(WalletTransactionRow.From).ToList());
    }

    /// <summary><paramref name="page"/> and <paramref name="limit"/> are JS parseInt results, NaN included.</summary>
    public async Task<TransactionsPage> GetTransactionsAsync(string userId, double page, double limit, CancellationToken ct)
    {
        var walletId = await db.Wallets.Where(w => w.UserId == userId).Select(w => w.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Wallet not found");

        // Prisma rejects a non-Int or negative skip and a non-Int take; Nest turns that into a 500.
        var skip = PrismaInt((page - 1) * limit, "skip");
        var take = PrismaInt(limit, "take");
        if (skip < 0)
            throw new InvalidOperationException($"skip must be positive, found {skip}");

        var query = db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == walletId);
        // A negative take counts from the end of the list, as in Prisma.
        var items = take >= 0
            ? await query.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(take).ToListAsync(ct)
            : (await query.OrderBy(t => t.CreatedAt).Skip(skip).Take(-take).ToListAsync(ct)).AsEnumerable().Reverse().ToList();
        var total = await query.CountAsync(ct);

        return new TransactionsPage(items.Select(WalletTransactionRow.From).ToList(), total, page, limit);
    }

    public async Task<DepositResponse> AdminDepositAsync(DepositDto dto, CancellationToken ct)
    {
        var amount = dto.Amount!.Value.TryGetDecimal(out var d) ? d : throw new InvalidOperationException("amount out of range");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var walletId = await db.Wallets.Where(w => w.UserId == dto.UserId.Text()).Select(w => w.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Wallet not found for user");
        var (wallet, transaction) = await CreditAsync(walletId, amount, WalletTransactionType.DEPOSIT,
            dto.Description.Text() ?? "Admin manual deposit", ct: ct);
        await tx.CommitAsync(ct);

        return new DepositResponse(WalletRow.From(wallet), WalletTransactionRow.From(transaction));
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to the wallet balance and writes the audit row. Runs in the caller's transaction,
    /// if any, and saves any other pending changes on the context with it.
    /// </summary>
    public async Task<(Wallet Wallet, WalletTransaction Transaction)> CreditAsync(
        string walletId, decimal amount, WalletTransactionType type, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default)
    {
        // A locking read: it sees the latest committed balance (a plain read in a REPEATABLE READ transaction can be stale)
        // and holds the row until commit, so concurrent credits write a consistent balanceBefore/balanceAfter chain.
        var locked = await db.Database
            .SqlQuery<decimal>($"SELECT balance AS `Value` FROM wallets WHERE id = {walletId} FOR UPDATE")
            .ToListAsync(ct);
        // Prisma findUniqueOrThrow: a missing row is an unhandled error, i.e. a 500.
        var balanceBefore = locked is [var b] ? b : throw new InvalidOperationException($"No Wallet found for id {walletId}");

        // Never read-modify-write the balance: the increment happens in SQL.
        await db.Wallets.Where(w => w.Id == walletId).ExecuteUpdateAsync(s => s
            .SetProperty(w => w.Balance, w => w.Balance + amount)
            .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);

        var row = new WalletTransaction
        {
            WalletId = walletId,
            Type = type,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + amount,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            Metadata = new RawJson(JsonSerializer.Serialize(new { description })),
        };
        db.WalletTransactions.Add(row);
        await db.SaveChangesAsync(ct);

        // Re-read both rows as stored (DECIMAL(15,2) and DATETIME(3) rounding), like Prisma's returned records.
        var wallet = await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId, ct);
        var transaction = await db.WalletTransactions.AsNoTracking().SingleAsync(t => t.Id == row.Id, ct);
        return (wallet, transaction);
    }

    private static int PrismaInt(double value, string argument) =>
        double.IsInteger(value) && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new InvalidOperationException($"Invalid value for {argument}: {value}");
}
