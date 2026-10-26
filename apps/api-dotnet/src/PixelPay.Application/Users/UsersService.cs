using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Auth;
using PixelPay.Application.Common;
using PixelPay.Domain.Entities;

namespace PixelPay.Application.Users;

public sealed class UsersService(IAppDbContext db, IPasswordHasher hasher)
{
    public async Task<ProfileResponse> GetProfileAsync(string id, CancellationToken ct)
    {
        var u = await Find(id, track: false, ct);
        return new ProfileResponse(u.Id, u.Email, u.Phone, u.DisplayName, u.AvatarUrl, u.Role.ToString(), u.IsVerified,
            u.ReferralCode, u.CreatedAt);
    }

    public async Task<UpdatedProfileResponse> UpdateProfileAsync(string id, UpdateProfileDto dto, CancellationToken ct)
    {
        var user = await Find(id, track: true, ct);

        // Prisma rejects NULL for the non-nullable displayName column, which Nest surfaces as a 500.
        if (dto.DisplayName.ValueKind == JsonValueKind.Null)
            throw new InvalidOperationException("displayName cannot be null");
        if (dto.DisplayName.ValueKind == JsonValueKind.String)
            user.DisplayName = dto.DisplayName.GetString()!;
        if (dto.AvatarUrl.ValueKind != JsonValueKind.Undefined)
            user.AvatarUrl = dto.AvatarUrl.GetString();

        await db.SaveChangesAsync(ct);
        return new UpdatedProfileResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl);
    }

    public async Task<MessageResponse> ChangePasswordAsync(string id, ChangePasswordDto dto, CancellationToken ct)
    {
        var user = await Find(id, track: true, ct);
        if (!hasher.Verify(dto.CurrentPassword.Text()!, user.PasswordHash))
            throw new BadRequestException("Current password is incorrect");

        user.PasswordHash = hasher.Hash(dto.NewPassword.Text()!);
        await db.SaveChangesAsync(ct);
        return new MessageResponse("Password changed successfully");
    }

    // Prisma findUniqueOrThrow: a missing row is an unhandled error, i.e. a 500.
    private async Task<User> Find(string id, bool track, CancellationToken ct) =>
        await (track ? db.Users : db.Users.AsNoTracking()).FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new InvalidOperationException($"No User found for id {id}");
}
