using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PixelPay.Api.Envelope;
using PixelPay.Infrastructure.Auth;
using PixelPay.Infrastructure.Config;
using PixelPay.Infrastructure.Realtime;

namespace PixelPay.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, EnvConfig env)
    {
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, NestAuthorizationResultHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = JwtTokenService.ValidationParameters(env.JwtSecret);
                o.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on a WebSocket, so the SignalR client sends the token in the query string.
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments(OrdersHub.Path)
                            && ctx.Request.Query.TryGetValue("access_token", out var token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
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
