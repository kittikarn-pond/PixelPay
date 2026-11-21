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

        var query = db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == walletId);
        var items = await PrismaPaging.NewestFirstAsync(query, t => t.CreatedAt, page, limit, ct);
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
    public Task<(Wallet Wallet, WalletTransaction Transaction)> CreditAsync(
        string walletId, decimal amount, WalletTransactionType type, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default) =>
        ApplyAsync(walletId, amount, type, description, referenceId, referenceType, amount, 0, Guard.None, null, ct);

    /// <summary>Subtracts from the spendable balance; 400 when it is too low.</summary>
    public Task<(Wallet Wallet, WalletTransaction Transaction)> DebitAsync(
        string walletId, decimal amount, WalletTransactionType type, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default) =>
        ApplyAsync(walletId, amount, type, description, referenceId, referenceType, -amount, 0, Guard.Balance,
            "Insufficient wallet balance", ct);

    /// <summary>Moves funds from balance to lockedBalance while an order is in flight (LOCK).</summary>
    public Task<(Wallet Wallet, WalletTransaction Transaction)> LockAsync(
        string walletId, decimal amount, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default) =>
        ApplyAsync(walletId, amount, WalletTransactionType.LOCK, description, referenceId, referenceType, -amount, amount,
            Guard.Balance, "Insufficient balance to lock", ct);

    /// <summary>Moves locked funds back to the spendable balance (refunds).</summary>
    public Task<(Wallet Wallet, WalletTransaction Transaction)> UnlockAsync(
        string walletId, decimal amount, WalletTransactionType type, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default) =>
        ApplyAsync(walletId, amount, type, description, referenceId, referenceType, amount, -amount, Guard.Locked,
            "Insufficient locked balance to unlock", ct);

    /// <summary>Spends locked funds when a top-up completes; the spendable balance is unchanged (TOPUP_DEBIT).</summary>
    public Task<(Wallet Wallet, WalletTransaction Transaction)> DebitLockedAsync(
        string walletId, decimal amount, string description,
        string? referenceId = null, string? referenceType = null, CancellationToken ct = default) =>
        ApplyAsync(walletId, amount, WalletTransactionType.TOPUP_DEBIT, description, referenceId, referenceType, 0, -amount,
            Guard.Locked, "Insufficient locked balance", ct);

    private enum Guard { None, Balance, Locked }

    private sealed record WalletBalances(decimal Balance, decimal LockedBalance);

    private async Task<(Wallet Wallet, WalletTransaction Transaction)> ApplyAsync(
        string walletId, decimal amount, WalletTransactionType type, string description, string? referenceId,
        string? referenceType, decimal balanceDelta, decimal lockedDelta, Guard guard, string? insufficient,
        CancellationToken ct) => await Tx.RunAsync(db, async () =>
    {
        // A locking read: it sees the latest committed balances (a plain read in a REPEATABLE READ transaction can be
        // stale) and holds the row until commit, so concurrent changes write a consistent before/after chain and the
        // sufficiency check below cannot race another debit.
        var locked = await db.Database
            .SqlQuery<WalletBalances>(
                $"SELECT balance AS Balance, locked_balance AS LockedBalance FROM wallets WHERE id = {walletId} FOR UPDATE")
            .ToListAsync(ct);
        // Prisma findUniqueOrThrow: a missing row is an unhandled error, i.e. a 500.
        var before = locked is [var b] ? b : throw new InvalidOperationException($"No Wallet found for id {walletId}");

        if ((guard == Guard.Balance && before.Balance < amount) || (guard == Guard.Locked && before.LockedBalance < amount))
            throw new BadRequestException(insufficient);

        // Never read-modify-write the balances: the arithmetic happens in SQL.
        // Capture the timestamp first: called inside the lambda, EF inlines it as a seconds-only TIMESTAMP literal.
        var now = PrismaTime.Now();
        await db.Wallets.Where(w => w.Id == walletId).ExecuteUpdateAsync(s => s
            .SetProperty(w => w.Balance, w => w.Balance + balanceDelta)
            .SetProperty(w => w.LockedBalance, w => w.LockedBalance + lockedDelta)
            .SetProperty(w => w.UpdatedAt, now), ct);

        var row = new WalletTransaction
        {
            WalletId = walletId,
            Type = type,
            Amount = amount,
            BalanceBefore = before.Balance,
            BalanceAfter = before.Balance + balanceDelta,
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
    }, ct);

    /// <summary>The wallet id for a user; Prisma findUniqueOrThrow semantics (500 when missing).</summary>
    public async Task<string> WalletIdForUserAsync(string userId, CancellationToken ct) =>
        await db.Wallets.Where(w => w.UserId == userId).Select(w => w.Id).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException($"No Wallet found for user {userId}");
}
