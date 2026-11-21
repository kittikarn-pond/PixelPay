using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Json;

namespace PixelPay.Api.Envelope;

public sealed record NestError(int Status, object Body) : IActionResult
{
    public Task WriteAsync(HttpContext context)
    {
        context.Response.StatusCode = Status;
        return context.Response.WriteAsJsonAsync(new { success = false, error = Body }, ApiJson.Options);
    }

    public Task ExecuteResultAsync(ActionContext context) => WriteAsync(context.HttpContext);
}
