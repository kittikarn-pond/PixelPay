namespace PixelPay.Api.Routing;

// Fastify answers HEAD on every GET route; the prod healthcheck (wget --spider) relies on it.
public sealed class HeadAsGetMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        var body = context.Response.Body;
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = Stream.Null;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = body;
            context.Request.Method = HttpMethods.Head;
        }
    }
}
