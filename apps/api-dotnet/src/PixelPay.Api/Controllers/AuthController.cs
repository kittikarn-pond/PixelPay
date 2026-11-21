using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Auth;

namespace PixelPay.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public Task<RegisterResponse> Register(RegisterDto dto, CancellationToken ct) => auth.RegisterAsync(dto, ct);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) => Ok(await auth.LoginAsync(request, ct));

    // No auth guard: the refresh token in the body is the credential and the access token may have expired.
    [HttpPost("refresh")]
    public IActionResult Refresh(RefreshTokenDto dto) => Ok(auth.Refresh(dto));

    [Authorize]
    [HttpGet("me")]
    public CurrentUser Me() => HttpContext.GetCurrentUser();

    [Authorize]
    [HttpPost("logout")]
    public MessageResponse Logout() => new("Logged out successfully");
}
