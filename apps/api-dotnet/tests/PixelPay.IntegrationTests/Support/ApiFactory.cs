using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using Testcontainers.MySql;

namespace PixelPay.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Database = "pixelpay_test";
    private const string User = "pixelpay";
    private const string Password = "pixelpay";

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.0")
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Password)
        .Build();

    public string ConnectionString => _mysql.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _mysql.StartAsync();
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(await File.ReadAllTextAsync(MigrationPath()), conn);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_URL",
            $"mysql://{User}:{Password}@{_mysql.Hostname}:{_mysql.GetMappedPublicPort(MySqlBuilder.MySqlPort)}/{Database}");
        builder.UseSetting("JWT_SECRET", "test_jwt_secret_64_chars_minimum_for_testing_purposes_only");
        builder.UseSetting("JWT_REFRESH_SECRET", "test_refresh_secret_64_chars_minimum_for_testing_purposes");
        builder.UseSetting("ENCRYPTION_KEY", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mysql.DisposeAsync();
    }

    private static string MigrationPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "apps", "api")))
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("Repo root with apps/api not found");
        return Path.Combine(dir.FullName, "apps", "api", "prisma", "migrations", "20260915040045_init", "migration.sql");
    }
}
