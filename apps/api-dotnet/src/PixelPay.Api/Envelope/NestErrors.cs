using Microsoft.AspNetCore.WebUtilities;
using PixelPay.Application.Common;

namespace PixelPay.Api.Envelope;

public static class NestErrors
{
    public const string InvalidJsonMessage = "Body is not valid JSON but content-type is set to 'application/json'";

    public static NestError Http(int status, string? message = null) => message is null
        ? new NestError(status, new { statusCode = status, message = ReasonPhrases.GetReasonPhrase(status) })
        : new NestError(status, new { statusCode = status, message, error = ReasonPhrases.GetReasonPhrase(status) });

    public static NestError From(ApiException ex) => Http(ex.Status, ex.ResponseMessage);

    public static NestError Validation(IEnumerable<string> messages) =>
        new(400, new { statusCode = 400, message = messages.ToArray(), error = "Bad Request" });

    public static NestError InvalidJson() => Http(400, InvalidJsonMessage);

    public static NestError Forbidden() => Http(403, "Forbidden resource");

    public static NestError RouteNotFound(string method, string path) => Http(404, $"Cannot {method} {path}");

    public static NestError Internal() => new(500, new { message = "Internal server error" });
}
