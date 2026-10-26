using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Envelope;

namespace PixelPay.Api.Validation;

public static class ModelStateErrors
{
    public static IActionResult ToNestError(ActionContext context)
    {
        var errors = context.ModelState.Values.SelectMany(v => v.Errors).ToList();

        // Nest maps a body-parser SyntaxError to BadRequestException(message): a string, not a list.
        return errors.Any(e => e.Exception is JsonException || e.ErrorMessage == NestErrors.InvalidJsonMessage)
            ? NestErrors.InvalidJson()
            : NestErrors.Validation(errors.Select(e => e.ErrorMessage));
    }
}
