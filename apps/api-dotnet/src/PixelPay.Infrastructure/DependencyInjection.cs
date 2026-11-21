using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PixelPay.Application.Admin;
using PixelPay.Application.Auth;
using PixelPay.Application.Catalog;
using PixelPay.Application.Common;
using PixelPay.Application.Notifications;
using PixelPay.Application.Orders;
using PixelPay.Application.Payments;
using PixelPay.Application.Promotions;
using PixelPay.Application.Users;
using PixelPay.Application.Wallets;
using PixelPay.Infrastructure.Auth;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure.External;
using PixelPay.Infrastructure.Jobs;
using PixelPay.Infrastructure.Persistence;
using PixelPay.Infrastructure.Realtime;
using StackExchange.Redis;

namespace PixelPay.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything the API and the worker share: config, database, application services, integrations, jobs, SignalR.</summary>
    public static IServiceCollection AddPixelPay(this IServiceCollection services, IConfiguration configuration, EnvConfig env)
    {
        var integrations = IntegrationsConfig.Load(configuration);
        services.AddSingleton(env);
        services.AddSingleton(integrations);
        services.TryAddSingleton(TimeProvider.System);
        services.AddPersistence(env);
        services.AddScoped<DbMigrator>();
        services.AddScoped<Seed.CatalogSeeder>();

        // Application services
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<AuthService>();
        services.AddScoped<UsersService>();
        services.AddScoped<WalletService>();
        services.AddScoped<OrdersQueryService>();
        services.AddScoped<OrdersService>();
        services.AddScoped<GamesService>();
        services.AddScoped<CategoriesService>();
        services.AddScoped<CouponsService>();
        services.AddScoped<CashbackService>();
        services.AddScoped<AffiliatesService>();
        services.AddScoped<ResellersService>();
        services.AddScoped<NotificationsService>();
        services.AddScoped<PaymentsService>();
        services.AddScoped<PaymentSettlement>();
        services.AddScoped<AdminService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<AuditService>();
        services.AddScoped<TopupJob>();
        services.AddScoped<PaymentPollJob>();
        services.AddScoped<NotificationDispatchJob>();

        // Integrations
        services.AddSingleton<ISecretBox, SecretBox>();
        services.AddHttpClient<IPaymentGateway, GbPrimePayGateway>(c =>
        {
            c.BaseAddress = new Uri(integrations.PaymentGatewayUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient("providers", c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<ITopupProviderFactory, TopupProviderFactory>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<ISmsSender, TwilioSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(15));

        // Redis: Hangfire storage and the SignalR backplane share one multiplexer.
        var redis = ConnectionMultiplexer.Connect(RedisUrl.ToConfiguration(env.RedisUrl));
        services.AddSingleton<IConnectionMultiplexer>(redis);
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseRedisStorage(redis, new RedisStorageOptions { Prefix = "pixelpay:hangfire:" }));
        services.AddScoped<IJobQueue, HangfireJobQueue>();
        services.AddScoped<TopupRunner>();
        services.AddScoped<PaymentPollRunner>();
        services.AddScoped<NotificationRunner>();

        services.AddSignalR().AddStackExchangeRedis(o =>
        {
            o.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(redis);
            o.Configuration.ChannelPrefix = RedisChannel.Literal("pixelpay:signalr");
        });
        services.AddSingleton<IOrderStatusNotifier, SignalROrderStatusNotifier>();
        return services;
    }
}
