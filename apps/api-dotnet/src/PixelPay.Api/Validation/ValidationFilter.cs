using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using PixelPay.Api.Envelope;

namespace PixelPay.Api.Validation;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var messages = new List<string>(StrictBodyFilter.Messages(context.HttpContext));

        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out var arg)
                || arg is null
                || context.HttpContext.RequestServices.GetService(typeof(IValidator<>).MakeGenericType(parameter.ParameterType))
                    is not IValidator validator)
                continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            messages.AddRange(result.Errors.Select(e => e.ErrorMessage));
        }

        if (messages.Count > 0)
            context.Result = NestErrors.Validation(messages);
        else
            await next();
    }
}
