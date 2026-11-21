using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using PixelPay.Application.Common;

namespace PixelPay.Api.Envelope;

public sealed class ExceptionEnvelopeMiddleware(RequestDelegate next, ILogger<ExceptionEnvelopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            if (ex is not ApiException)
                logger.LogError(ex, "Unhandled exception");
            context.Response.Clear();
            await (ex is ApiException api ? NestErrors.From(api) : NestErrors.Internal()).WriteAsync(context);
            return;
        }

        if (IsUnmatchedRoute(context))
            await NestErrors.RouteNotFound(context.Request.Method, RequestedPath(context)).WriteAsync(context);
    }

    // ASP.NET answers a known path with the wrong method with 405; Fastify answers 404.
    private static bool IsUnmatchedRoute(HttpContext context) =>
        !context.Response.HasStarted
        && context.Response.StatusCode is StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed
        && context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is null;

    // Fastify's raw.url: undecoded, query string included.
    private static string RequestedPath(HttpContext context)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        return string.IsNullOrEmpty(raw)
            ? $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}"
            : raw;
    }
}
