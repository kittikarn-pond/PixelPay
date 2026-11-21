using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Config;

namespace PixelPay.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, EnvConfig env)
    {
        services.AddSingleton(_ => new MySqlDataSource(PixelPayDbContext.ConnectionString(env.ConnectionString)));
        services.AddDbContext<PixelPayDbContext>(o => PixelPayDbContext.Configure(o, env.ConnectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<PixelPayDbContext>());
        return services;
    }
}
