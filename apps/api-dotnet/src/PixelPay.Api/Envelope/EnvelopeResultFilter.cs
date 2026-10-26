using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PixelPay.Api.Envelope;

public sealed class EnvelopeResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        switch (context.Result)
        {
            case ObjectResult { StatusCode: null or < 400 } result:
                context.Result = Success(context, result.Value, result.StatusCode);
                break;
            case EmptyResult:
                context.Result = Success(context, null, null);
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    // Nest answers POST with 201 unless the handler sets another code; Ok(...) is the @HttpCode(200) equivalent.
    private static ObjectResult Success(ResultExecutingContext context, object? data, int? status) =>
        new(new { success = true, data })
        {
            StatusCode = status ?? (HttpMethods.IsPost(context.HttpContext.Request.Method) ? 201 : 200),
        };
}
