using System.Text.Json;

namespace PixelPay.Application.Auth;

// Request fields are JsonElement? so a non-string value reaches the validator (ruling R9).
public sealed record RegisterDto(JsonElement? Email, JsonElement? Password, JsonElement? DisplayName, JsonElement? ReferredBy);

// No validator: passport-local reads the body as is (ruling R1).
public sealed record LoginRequest(JsonElement? Email, JsonElement? Password);

public sealed record RefreshTokenDto(JsonElement? RefreshToken);

public sealed record RegisteredUser(string Id, string Email, string DisplayName, string Role, string ReferralCode);

public sealed record RegisterResponse(RegisteredUser User, string AccessToken, string RefreshToken);

public sealed record LoginUser(string Id, string Email, string Role);

public sealed record LoginResponse(LoginUser User, string AccessToken, string RefreshToken);

public sealed record MessageResponse(string Message);
