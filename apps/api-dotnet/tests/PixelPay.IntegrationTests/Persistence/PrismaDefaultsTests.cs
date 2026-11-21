using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using PixelPay.Domain.Entities;
using PixelPay.Domain.Enums;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Persistence;

public sealed class PrismaDefaultsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<object?> Scalar(string sql)
    {
        await using var conn = new MySqlConnection(factory.ConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new MySqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync(Ct);
    }

    private async Task Exec(string sql) => await Scalar(sql);

    [Fact]
    public async Task User_with_only_required_fields_gets_prisma_defaults()
    {
        var user = new User { Email = "d@b.c", PasswordHash = "h", DisplayName = "D", ReferralCode = "DEF1", Wallet = new Wallet() };
        await using (var db = factory.Db())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(36, user.Id.Length);
        Assert.Equal(user.Id.ToLowerInvariant(), user.Id);
        Assert.Equal(1L, Convert.ToInt64(await Scalar($"SELECT is_active FROM users WHERE id = '{user.Id}'")));
        Assert.Equal("USER", await Scalar($"SELECT role FROM users WHERE id = '{user.Id}'"));
        Assert.Equal("THB", await Scalar($"SELECT currency FROM wallets WHERE user_id = '{user.Id}'"));
    }

    [Fact]
    public async Task Order_gets_json_and_numeric_defaults()
    {
        var user = new User { Email = "o@b.c", PasswordHash = "h", DisplayName = "O", ReferralCode = "DEF2" };
        var product = new GameProduct
        {
            Name = "p", Sku = "SKU1", PriceCost = 1m, PriceSell = 2m,
            Game = new Game { Name = "g", Slug = "g1" },
        };
        var order = new Order
        {
            OrderNumber = "ORD1", User = user, GameProduct = product, UnitPrice = 2m, TotalPrice = 2m,
            PaymentMethod = PaymentMethod.WALLET, GameUid = "uid",
        };
        await using (var db = factory.Db())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal("{}", await Scalar($"SELECT CAST(metadata AS CHAR) FROM orders WHERE id = '{order.Id}'"));
        Assert.Equal(1, Convert.ToInt32(await Scalar($"SELECT quantity FROM orders WHERE id = '{order.Id}'")));
        Assert.Equal("PENDING", await Scalar($"SELECT status FROM orders WHERE id = '{order.Id}'"));
        Assert.Equal("{}", await Scalar($"SELECT CAST(metadata AS CHAR) FROM game_products WHERE id = '{product.Id}'"));
        Assert.Equal("THB", await Scalar($"SELECT currency FROM game_products WHERE id = '{product.Id}'"));

        // nullable DateTime read comes back as Utc
        await Exec($"UPDATE orders SET completed_at = '2026-05-06 07:08:09.123' WHERE id = '{order.Id}'");
        await using var read = factory.Db();
        var loaded = await read.Orders.SingleAsync(o => o.Id == order.Id, Ct);
        Assert.NotNull(loaded.CompletedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CompletedAt.Value.Kind);
        Assert.Equal(new DateTime(2026, 5, 6, 7, 8, 9, 123, DateTimeKind.Utc), loaded.CompletedAt);
    }

    [Fact]
    public async Task Save_stamps_updated_at_on_modify()
    {
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var user = new User { Email = "u@b.c", PasswordHash = "h", DisplayName = "U", ReferralCode = "DEF3", UpdatedAt = old };
        await using (var db = factory.Db())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
            Assert.True(user.UpdatedAt > old);
        }

        await Exec($"UPDATE users SET updated_at = '2020-01-01 00:00:00.000' WHERE id = '{user.Id}'");

        await using var db2 = factory.Db();
        var loaded = await db2.Users.SingleAsync(u => u.Id == user.Id, Ct);
        Assert.Equal(old, loaded.UpdatedAt);
        loaded.DisplayName = "U2";
        await db2.SaveChangesAsync(Ct);

        await using var db3 = factory.Db();
        Assert.True((await db3.Users.SingleAsync(u => u.Id == user.Id, Ct)).UpdatedAt > old.AddYears(1));
    }

    [Fact]
    public async Task Writing_local_datetime_throws()
    {
        await using var db = factory.Db();
        db.Users.Add(new User
        {
            Email = "l@b.c", PasswordHash = "h", DisplayName = "L", ReferralCode = "DEF4",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local),
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
