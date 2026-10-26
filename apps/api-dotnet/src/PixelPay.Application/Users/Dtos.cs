using System.Text.Json;

namespace PixelPay.Application.Users;

// Not nullable on purpose: a missing field stays Undefined while an explicit null is kept as Null,
// and Prisma treats the two differently (absent = untouched, null = set NULL).
public sealed record UpdateProfileDto(JsonElement DisplayName, JsonElement AvatarUrl);

public sealed record ChangePasswordDto(JsonElement? CurrentPassword, JsonElement? NewPassword);

public sealed record ProfileResponse(
    string Id, string Email, string? Phone, string DisplayName, string? AvatarUrl,
    string Role, bool IsVerified, string ReferralCode, DateTime CreatedAt);

public sealed record UpdatedProfileResponse(string Id, string Email, string DisplayName, string? AvatarUrl);
