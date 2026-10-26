using System.Security.Cryptography;
using System.Text.Json;
using PixelPay.Infrastructure.Security;

namespace PixelPay.UnitTests.Security;

public class CryptoUtilTests
{
    private sealed record Vectors(
        string KeyHex, string Plaintext, string Ciphertext, string HmacSecret, string HmacData, string HmacHex);

    private static readonly Vectors V = Load();

    private static Vectors Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Security", "fixtures", "node-vectors.json");
        return JsonSerializer.Deserialize<Vectors>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public void Decrypts_node_ciphertext() =>
        Assert.Equal(V.Plaintext, CryptoUtil.DecryptAes256(V.KeyHex, V.Ciphertext));

    [Fact]
    public void Hmac_matches_node() =>
        Assert.Equal(V.HmacHex, CryptoUtil.HmacSha256Hex(V.HmacSecret, V.HmacData));

    [Fact]
    public void Roundtrip()
    {
        var encrypted = CryptoUtil.EncryptAes256(V.KeyHex, V.Plaintext);
        var parts = encrypted.Split(':');
        Assert.Equal(3, parts.Length);
        Assert.Equal(24, parts[0].Length);
        Assert.Equal(32, parts[1].Length);
        Assert.Equal(V.Plaintext, CryptoUtil.DecryptAes256(V.KeyHex, encrypted));
    }

    [Fact]
    public void Tampered_tag_throws()
    {
        var parts = V.Ciphertext.Split(':');
        parts[1] = (parts[1][0] == '0' ? "1" : "0") + parts[1][1..];
        Assert.Throws<AuthenticationTagMismatchException>(
            () => CryptoUtil.DecryptAes256(V.KeyHex, string.Join(':', parts)));
    }

    [Theory]
    [InlineData("onlyonepart")]
    [InlineData("aa:bb")]
    [InlineData("zz:yy:xx")]
    public void Malformed_input_throws(string input) =>
        Assert.ThrowsAny<SystemException>(() => CryptoUtil.DecryptAes256(V.KeyHex, input));
}
