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
            // 204 cannot carry a body (Fastify strips it too).
            case StatusCodeResult { StatusCode: < 300 and not StatusCodes.Status204NoContent } result:
                context.Result = Success(context, null, result.StatusCode);
                break;
            case StatusCodeResult { StatusCode: >= 400 } result:
                context.Result = NestErrors.Http(result.StatusCode);
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
