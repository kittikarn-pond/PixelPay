using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PixelPay.Api.Envelope;
using PixelPay.Application.Auth;
using PixelPay.Application.Common;
using PixelPay.Infrastructure.Auth;
using PixelPay.Infrastructure.Config;

namespace PixelPay.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, EnvConfig env)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<AuthService>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, NestAuthorizationResultHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = JwtTokenService.ValidationParameters(env.JwtSecret);
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        return NestErrors.Http(StatusCodes.Status401Unauthorized).WriteAsync(ctx.HttpContext);
                    },
                };
            });
        services.AddAuthorization();
        return services;
    }
}

/// <summary>Writes Nest's JwtAuthGuard 401 and RolesGuard 403 bodies instead of the scheme's challenge/forbid.</summary>
internal sealed class NestAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Challenged)
            return NestErrors.Http(StatusCodes.Status401Unauthorized).WriteAsync(context);
        if (result.Forbidden)
            return NestErrors.Forbidden().WriteAsync(context);
        return _default.HandleAsync(next, context, policy, result);
    }
}
