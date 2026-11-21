using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PixelPay.Api.Validation;

// Nest sees a body without a JSON content type, or a literal JSON null, as {} and validates that,
// where MVC would answer 415 or bind null.
public sealed class NonJsonBodyFilter : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (context.ActionDescriptor.Parameters.Any(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            && (!request.HasJsonContentType() || await IsJsonNull(request)))
        {
            request.ContentType = "application/json";
            request.ContentLength = 2;
            request.Body = new MemoryStream("{}"u8.ToArray());
        }

        await next();
    }

    private static async Task<bool> IsJsonNull(HttpRequest request)
    {
        request.EnableBuffering();
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            return doc.RootElement.ValueKind == JsonValueKind.Null;
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
