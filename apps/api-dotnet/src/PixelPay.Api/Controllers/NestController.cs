using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Common;

namespace PixelPay.Api.Controllers;

[ApiController]
public abstract class NestController : ControllerBase
{
    protected string UserId => HttpContext.GetCurrentUser().Id;

    /// <summary>Unary + on a query value, like Node: the default applies only when the key is absent; a repeated key is NaN.</summary>
    protected double Number(string key, double fallback) =>
        !Request.Query.TryGetValue(key, out var v) ? fallback : v.Count == 1 ? Js.ToNumber(v[0]!) : double.NaN;

    protected string? Text(string key) => Request.Query.TryGetValue(key, out var v) ? v.ToString() : null;
}
