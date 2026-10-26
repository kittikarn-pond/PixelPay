using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Persistence;

public sealed class DbContextTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Exec(string sql)
    {
        await using var conn = new MySqlConnection(factory.ConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    [Fact]
    public async Task Reads_prisma_rows_with_enums_and_utc()
    {
        await Exec("""
            INSERT INTO users (id, email, password_hash, display_name, role, referral_code, created_at, updated_at)
            VALUES ('11111111-1111-1111-1111-111111111111', 'a@b.c', 'h', 'A', 'USER', 'REF1', '2026-01-02 03:04:05.678', '2026-01-02 03:04:05.678');
            INSERT INTO wallets (id, user_id, updated_at)
            VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '2026-01-02 03:04:05.678');
            """);

        await using var db = factory.Db();
        var user = await db.Users.Include(u => u.Wallet).SingleAsync(u => u.Email == "a@b.c", Ct);

        Assert.Equal("11111111-1111-1111-1111-111111111111", user.Id);
        Assert.Equal(UserRole.USER, user.Role);
        Assert.True(user.IsActive);
        Assert.NotNull(user.Wallet);
        Assert.Equal(0m, user.Wallet.Balance);
        Assert.Equal("THB", user.Wallet.Currency);
        Assert.Equal(DateTimeKind.Utc, user.Wallet.UpdatedAt.Kind);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc), user.Wallet.UpdatedAt);
    }

    [Fact]
    public async Task Writes_enums_by_name_json_raw_and_utc_as_is()
    {
        var walletId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var at = new DateTime(2026, 3, 4, 5, 6, 7, 890, DateTimeKind.Utc);

        await using (var db = factory.Db())
        {
            db.Users.Add(new User
            {
                Id = userId, Email = "w@b.c", PasswordHash = "h", DisplayName = "W", Role = UserRole.ADMIN,
                IsActive = false, ReferralCode = "REF2", CreatedAt = at, UpdatedAt = at,
                Wallet = new Wallet { Id = walletId, UpdatedAt = at },
            });
            db.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid().ToString(), WalletId = walletId, Type = WalletTransactionType.TOPUP_DEBIT,
                Amount = 12.5m, BalanceBefore = 20m, BalanceAfter = 7.5m, CreatedAt = at,
                Metadata = new RawJson("""{"a":1}"""),
            });
            await db.SaveChangesAsync(Ct);
        }

        await using var conn = new MySqlConnection(factory.ConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new MySqlCommand("""
            SELECT u.role, u.is_active, CAST(wt.type AS CHAR), CAST(wt.metadata AS CHAR), DATE_FORMAT(u.created_at, '%Y-%m-%d %H:%i:%s.%f')
            FROM users u JOIN wallets w ON w.user_id = u.id JOIN wallet_transactions wt ON wt.wallet_id = w.id
            WHERE u.id = @id
            """, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var r = await cmd.ExecuteReaderAsync(Ct);
        Assert.True(await r.ReadAsync(Ct));
        Assert.Equal("ADMIN", r.GetString(0));
        Assert.False(r.GetBoolean(1));
        Assert.Equal("TOPUP_DEBIT", r.GetString(2));
        Assert.Equal("""{"a": 1}""", r.GetString(3));
        Assert.Equal("2026-03-04 05:06:07.890000", r.GetString(4));
        await r.CloseAsync();

        await using var read = factory.Db();
        var tx = await read.WalletTransactions.SingleAsync(t => t.WalletId == walletId, Ct);
        Assert.Equal(WalletTransactionType.TOPUP_DEBIT, tx.Type);
        Assert.Equal(12.5m, tx.Amount);
        Assert.NotNull(tx.Metadata);
        Assert.Contains("\"a\"", tx.Metadata.Value.Value);
        Assert.Equal(DateTimeKind.Utc, tx.CreatedAt.Kind);
        Assert.Equal(at, tx.CreatedAt);
    }
}
