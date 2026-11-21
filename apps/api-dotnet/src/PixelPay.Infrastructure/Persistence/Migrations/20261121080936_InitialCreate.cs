using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PixelPay.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The schema as Prisma's only migration (20260915040045_init) created it, statement for statement, so a fresh
    /// database is identical to the ones already in use. Databases Prisma created are baselined: DbMigrator records
    /// this migration as applied without running it.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        private static readonly string[] Tables =
        [
            "audit_logs", "notifications", "affiliate_commissions", "affiliates", "cashback_rules", "coupon_usages",
            "payments", "orders", "coupons", "provider_products", "providers", "game_products", "games", "categories",
            "resellers", "wallet_transactions", "wallets", "users",
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in Statements(Baseline()))
                migrationBuilder.Sql(statement);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 0;");
            foreach (var table in Tables)
                migrationBuilder.Sql($"DROP TABLE IF EXISTS `{table}`;");
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 1;");
        }

        internal static string Baseline()
        {
            var assembly = typeof(InitialCreate).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("20261121080936_InitialCreate.sql", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>One statement per command; the file holds only DDL, so splitting on ';' line ends is safe.</summary>
        internal static IEnumerable<string> Statements(string sql) =>
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split(";\n")
                .Select(s => string.Join('\n', s.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal))).Trim())
                .Where(s => s.Length > 0)
                .Select(s => s.EndsWith(';') ? s : s + ";");
    }
}
