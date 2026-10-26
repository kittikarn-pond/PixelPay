using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;

namespace PixelPay.IntegrationTests.Support;

[ApiController]
[Route("api/v1/auth-test")]
public sealed class AuthTestController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public CurrentUser Me() => HttpContext.GetCurrentUser();

    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    public object Admin() => new { ok = true };
}
