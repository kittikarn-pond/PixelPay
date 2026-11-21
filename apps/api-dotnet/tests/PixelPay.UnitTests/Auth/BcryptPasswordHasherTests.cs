using PixelPay.Infrastructure.Auth;

namespace PixelPay.UnitTests.Auth;

public class BcryptPasswordHasherTests
{
    // node -e "require('bcrypt').hash('Test1234!',12).then(console.log)"
    private const string NodeHash = "$2b$12$4wmjOw0U6aKLU41TZmOsGObLgqooGarclnOHiPTDR1ewJO3Ul5MjC";

    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_2b_cost_12_and_verifies()
    {
        var hash = _hasher.Hash("Test1234!");

        Assert.StartsWith("$2b$12$", hash);
        Assert.True(_hasher.Verify("Test1234!", hash));
        Assert.False(_hasher.Verify("Test1234?", hash));
    }

    [Fact]
    public void Verifies_node_hash()
    {
        Assert.True(_hasher.Verify("Test1234!", NodeHash));
        Assert.False(_hasher.Verify("wrongpass", NodeHash));
    }

    [Fact]
    public void Malformed_hash_is_false() => Assert.False(_hasher.Verify("Test1234!", "not-a-hash"));
}
