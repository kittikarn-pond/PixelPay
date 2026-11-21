using BCrypt.Net;
using PixelPay.Application.Common;

namespace PixelPay.Infrastructure.Auth;

/// <summary>Node's bcrypt: cost 12, <c>$2b$</c> hashes.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int Cost = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(Cost, 'b'));

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            // bcrypt.compare resolves false for a malformed hash.
            return false;
        }
    }
}
