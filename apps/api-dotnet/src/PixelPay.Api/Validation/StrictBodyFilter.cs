using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PixelPay.Api.Json;

namespace PixelPay.Api.Validation;

// Nest's forbidNonWhitelisted, applied only to bodies whose DTO has a validator.
public sealed class StrictBodyFilter : IAsyncResourceFilter
{
    private const string ItemKey = "PixelPay.StrictBodyMessages";

    public static IReadOnlyList<string> Messages(HttpContext context) =>
        context.Items[ItemKey] as IReadOnlyList<string> ?? [];

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var bodyType = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body)?.ParameterType;
        var http = context.HttpContext;

        if (bodyType is not null && http.RequestServices.GetService(typeof(IValidator<>).MakeGenericType(bodyType)) is not null)
            http.Items[ItemKey] = await UnknownProperties(http.Request, bodyType);

        await next();
    }

    private static async Task<IReadOnlyList<string>> UnknownProperties(HttpRequest request, Type bodyType)
    {
        request.EnableBuffering();
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return [];
            var known = ApiJson.Options.GetTypeInfo(bodyType).Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            return doc.RootElement.EnumerateObject()
                .Where(p => !known.Contains(p.Name))
                .Select(p => $"property {p.Name} should not exist")
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
