using System.Security.Cryptography;

namespace PixelPay.Application.Auth;

public static class ReferralCode
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, 8);
}
