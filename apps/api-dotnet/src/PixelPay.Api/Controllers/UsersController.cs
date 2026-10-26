using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Auth;
using PixelPay.Application.Users;

namespace PixelPay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController(UsersService users) : ControllerBase
{
    [HttpGet("profile")]
    public Task<ProfileResponse> Profile(CancellationToken ct) => users.GetProfileAsync(HttpContext.GetCurrentUser().Id, ct);

    [HttpPatch("profile")]
    public Task<UpdatedProfileResponse> UpdateProfile(UpdateProfileDto dto, CancellationToken ct) =>
        users.UpdateProfileAsync(HttpContext.GetCurrentUser().Id, dto, ct);

    [HttpPut("change-password")]
    public Task<MessageResponse> ChangePassword(ChangePasswordDto dto, CancellationToken ct) =>
        users.ChangePasswordAsync(HttpContext.GetCurrentUser().Id, dto, ct);
}
