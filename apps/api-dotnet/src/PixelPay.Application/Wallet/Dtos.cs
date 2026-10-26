using System.Text.Json;
using PixelPay.Domain.Common;
using PixelPay.Domain.Enums;
using PixelPay.Domain.Entities;

// "Wallets", not "Wallet": a Wallet namespace would hide the Wallet entity in sibling namespaces.
namespace PixelPay.Application.Wallets;

// Loose fields so wrong JSON types reach the validator (ruling R9).
public sealed record DepositDto(JsonElement? UserId, JsonElement? Amount, JsonElement? Description);

// Prisma row shapes: every column in schema order, nulls included.
public sealed record WalletRow(string Id, string UserId, decimal Balance, decimal LockedBalance, string Currency, DateTime UpdatedAt)
{
    public static WalletRow From(Wallet w) => new(w.Id, w.UserId, w.Balance, w.LockedBalance, w.Currency, w.UpdatedAt);
}

public sealed record WalletWithTransactions(
    string Id, string UserId, decimal Balance, decimal LockedBalance, string Currency, DateTime UpdatedAt,
    IReadOnlyList<WalletTransactionRow> Transactions);

public sealed record WalletTransactionRow(
    string Id, string WalletId, WalletTransactionType Type, decimal Amount, decimal BalanceBefore, decimal BalanceAfter,
    string? ReferenceId, string? ReferenceType, RawJson? Metadata, DateTime CreatedAt)
{
    public static WalletTransactionRow From(WalletTransaction t) =>
        new(t.Id, t.WalletId, t.Type, t.Amount, t.BalanceBefore, t.BalanceAfter, t.ReferenceId, t.ReferenceType, t.Metadata, t.CreatedAt);
}

// page and limit are JS numbers from parseInt.
public sealed record TransactionsPage(IReadOnlyList<WalletTransactionRow> Items, int Total, double Page, double Limit);

public sealed record DepositResponse(WalletRow Wallet, WalletTransactionRow Transaction);
