using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Persistence;
using Testcontainers.MySql;
using Testcontainers.Redis;

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

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public string ConnectionString => _mysql.GetConnectionString();

    /// <summary>Jobs the API enqueued; tests run them in-process with <see cref="RunJobsAsync"/>.</summary>
    public RecordingJobQueue Jobs { get; } = new();

    public FakeTopupProviders Providers { get; } = new();

    public FakePaymentGateway PaymentGateway { get; } = new();

    public RecordingOrderNotifier OrderNotifier { get; } = new();

    public PixelPayDbContext Db() => PixelPayDbContext.Create(ConnectionString);

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());
        // The real migration path: EF Core migrations, as `PixelPay.Api migrate` runs them.
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_URL",
            $"mysql://{User}:{Password}@{_mysql.Hostname}:{_mysql.GetMappedPublicPort(MySqlBuilder.MySqlPort)}/{Database}");
        builder.UseSetting("REDIS_URL", $"redis://{_redis.Hostname}:{_redis.GetMappedPublicPort(RedisBuilder.RedisPort)}");
        builder.UseSetting("JWT_SECRET", "test_jwt_secret_64_chars_minimum_for_testing_purposes_only");
        builder.UseSetting("JWT_REFRESH_SECRET", "test_refresh_secret_64_chars_minimum_for_testing_purposes");
        builder.UseSetting("ENCRYPTION_KEY", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IJobQueue>(Jobs);
            services.AddSingleton<ITopupProviderFactory>(Providers);
            services.AddSingleton<IPaymentGateway>(PaymentGateway);
            services.AddSingleton<IOrderStatusNotifier>(OrderNotifier);
            services.AddSingleton<IEmailSender, NullEmailSender>();
        });
    }

    /// <summary>Runs every queued job (and any it queues in turn) in a fresh scope, like the worker would.</summary>
    public async Task RunJobsAsync(int max = 200)
    {
        for (var i = 0; i < max && Jobs.TryDequeue(out var job); i++)
        {
            await using var scope = Services.CreateAsyncScope();
            await job(scope.ServiceProvider);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mysql.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
