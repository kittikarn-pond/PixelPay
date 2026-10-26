using System.Security.Cryptography;
using System.Text;

namespace PixelPay.Infrastructure.Security;

/// <summary>Byte-compatible with apps/api/src/common/utils/crypto.ts.</summary>
public static class CryptoUtil
{
    private const int IvSize = 12;
    private const int TagSize = 16;

    public static string HmacSha256Hex(string secret, string data) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));

    public static string EncryptAes256(string keyHex, string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = new byte[data.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(Convert.FromHexString(keyHex), TagSize);
        aes.Encrypt(iv, data, encrypted, tag);
        return $"{Convert.ToHexStringLower(iv)}:{Convert.ToHexStringLower(tag)}:{Convert.ToHexStringLower(encrypted)}";
    }

    public static string DecryptAes256(string keyHex, string ciphertext)
    {
        var parts = ciphertext.Split(':');
        if (parts.Length != 3)
            throw new FormatException("Ciphertext must be ivHex:tagHex:dataHex.");

        var iv = Convert.FromHexString(parts[0]);
        var tag = Convert.FromHexString(parts[1]);
        var data = Convert.FromHexString(parts[2]);
        var plain = new byte[data.Length];
        using var aes = new AesGcm(Convert.FromHexString(keyHex), TagSize);
        aes.Decrypt(iv, data, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
