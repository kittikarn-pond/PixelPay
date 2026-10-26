using Microsoft.Extensions.Configuration;
using PixelPay.Infrastructure.Config;

namespace PixelPay.UnitTests.Config;

public class EnvConfigTests
{
    private static readonly Dictionary<string, string?> Required = new()
    {
        ["DATABASE_URL"] = "mysql://u:p@h/db",
        ["JWT_SECRET"] = "jwt",
        ["JWT_REFRESH_SECRET"] = "refresh",
        ["ENCRYPTION_KEY"] = "key",
    };

    private static EnvConfig Load(Dictionary<string, string?> values) =>
        EnvConfig.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Applies_defaults()
    {
        var env = Load(Required);

        Assert.Equal("15m", env.JwtExpiresIn);
        Assert.Equal("7d", env.JwtRefreshExpiresIn);
        Assert.Equal(3000, env.Port);
        Assert.Equal("http://localhost:3001", env.FrontendUrl);
        Assert.Equal("Server=h;Port=3306;Database=db;User ID=u;Password=p;", env.ConnectionString);
    }

    [Theory]
    [InlineData("DATABASE_URL")]
    [InlineData("JWT_SECRET")]
    [InlineData("JWT_REFRESH_SECRET")]
    [InlineData("ENCRYPTION_KEY")]
    public void Missing_required_var_throws(string name)
    {
        var values = new Dictionary<string, string?>(Required) { [name] = null };

        var ex = Assert.Throws<InvalidOperationException>(() => Load(values));
        Assert.Equal($"{name} is required", ex.Message);
    }

    [Fact]
    public void Non_integer_port_throws()
    {
        var values = new Dictionary<string, string?>(Required) { ["APP_PORT"] = "abc" };

        var ex = Assert.Throws<InvalidOperationException>(() => Load(values));
        Assert.Equal("APP_PORT must be an integer", ex.Message);
    }
}
