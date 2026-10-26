using PixelPay.Application.Common;

namespace PixelPay.Api.Auth;

/// <summary>The authenticated caller, as Node's JwtStrategy.validate returns it.</summary>
public sealed record CurrentUser(string Id, string Email, string Role);

public static class CurrentUserExtensions
{
    /// <summary>Use only on [Authorize] actions; throws 401 when the request carries no valid user.</summary>
    public static CurrentUser GetCurrentUser(this HttpContext context)
    {
        var user = context.User;
        var id = user.FindFirst("sub")?.Value;
        var email = user.FindFirst("email")?.Value;
        var role = user.FindFirst("role")?.Value;
        return id is null || email is null || role is null
            ? throw new UnauthorizedException()
            : new CurrentUser(id, email, role);
    }
}
