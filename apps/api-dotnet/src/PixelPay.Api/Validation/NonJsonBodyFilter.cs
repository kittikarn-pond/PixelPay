using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PixelPay.Api.Validation;

// Nest sees a body without a JSON content type as {} and validates that, where MVC would answer 415.
public sealed class NonJsonBodyFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.HasJsonContentType()
            || !context.ActionDescriptor.Parameters.Any(p => p.BindingInfo?.BindingSource == BindingSource.Body))
            return;

        request.ContentType = "application/json";
        request.ContentLength = 2;
        request.Body = new MemoryStream("{}"u8.ToArray());
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
