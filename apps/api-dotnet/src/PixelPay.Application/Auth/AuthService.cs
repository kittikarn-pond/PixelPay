using Microsoft.EntityFrameworkCore;
using PixelPay.Application.Common;
using PixelPay.Domain.Entities;

namespace PixelPay.Application.Auth;

public sealed class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens)
{
    private const int ReferralCodeAttempts = 5;

    public async Task<RegisterResponse> RegisterAsync(RegisterDto dto, CancellationToken ct)
    {
        var email = dto.Email.Text()!;
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Email already registered");

        var passwordHash = hasher.Hash(dto.Password.Text()!);

        User? referrer = null;
        if (dto.ReferredBy.Text() is { Length: > 0 } referredBy)
            referrer = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ReferralCode == referredBy, ct);

        var user = await CreateUserWithWallet(email, passwordHash, dto.DisplayName.Text()!, referrer?.Id, ct);

        if (referrer is not null)
            await CountReferral(referrer, ct);

        var pair = tokens.Issue(user.Id, user.Email, user.Role.ToString());
        return new RegisterResponse(
            new RegisteredUser(user.Id, user.Email, user.DisplayName, user.Role.ToString(), user.ReferralCode),
            pair.AccessToken, pair.RefreshToken);
    }

    /// <summary>passport-local plus Node's validateUser: missing fields give a bare 401.</summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (request.Email.Text() is not { Length: > 0 } email || request.Password.Text() is not { Length: > 0 } password)
            throw new UnauthorizedException();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        var role = user.Role.ToString();
        var pair = tokens.Issue(user.Id, user.Email, role);
        return new LoginResponse(new LoginUser(user.Id, user.Email, role), pair.AccessToken, pair.RefreshToken);
    }

    public TokenPair Refresh(RefreshTokenDto dto)
    {
        if (tokens.ValidateRefresh(dto.RefreshToken.Text()!) is not { } p)
            throw new UnauthorizedException("Invalid or expired refresh token");
        return tokens.Issue(p.Sub, p.Email, p.Role);
    }

    // User and wallet are inserted by one SaveChanges, i.e. one transaction.
    private async Task<User> CreateUserWithWallet(string email, string passwordHash, string displayName, string? referredBy,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                DisplayName = displayName,
                ReferralCode = ReferralCode.New(),
                ReferredBy = referredBy,
            };
            var wallet = new Wallet { UserId = user.Id, Balance = 0, LockedBalance = 0 };
            db.Users.Add(user);
            db.Wallets.Add(wallet);
            try
            {
                await db.SaveChangesAsync(ct);
                return user;
            }
            catch (DbUpdateException)
            {
                db.Wallets.Remove(wallet);
                db.Users.Remove(user);
                // A concurrent register won the unique email index.
                if (await db.Users.AnyAsync(u => u.Email == email, ct))
                    throw new ConflictException("Email already registered");
                // Otherwise the random referral code hit the unique index.
                if (attempt == ReferralCodeAttempts || !await db.Users.AnyAsync(u => u.ReferralCode == user.ReferralCode, ct))
                    throw;
            }
        }
    }

    // Prisma upsert: increment the referrer's affiliate row, creating it on the first referral.
    private async Task CountReferral(User referrer, CancellationToken ct)
    {
        if (await IncrementReferrals(referrer.Id, ct))
            return;

        var affiliate = new Affiliate
        {
            UserId = referrer.Id,
            ReferralCode = referrer.ReferralCode,
            TotalReferrals = 1,
            TotalEarnings = 0,
            PendingEarnings = 0,
        };
        db.Affiliates.Add(affiliate);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent register created the row first.
            db.Affiliates.Remove(affiliate);
            if (!await IncrementReferrals(referrer.Id, ct))
                throw;
        }
    }

    private async Task<bool> IncrementReferrals(string userId, CancellationToken ct) =>
        await db.Affiliates
            .Where(a => a.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.TotalReferrals, a => a.TotalReferrals + 1), ct) > 0;
}
