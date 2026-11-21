namespace PixelPay.Application.Auth;

public sealed record TokenPair(string AccessToken, string RefreshToken);

public interface ITokenService
{
    TokenPair Issue(string userId, string email, string role);

    (string Sub, string Email, string Role)? ValidateRefresh(string token);
}
