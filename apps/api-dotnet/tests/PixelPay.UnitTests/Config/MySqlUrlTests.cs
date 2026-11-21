using MySqlConnector;
using PixelPay.Infrastructure.Config;

namespace PixelPay.UnitTests.Config;

public class MySqlUrlTests
{
    [Fact]
    public void Parses_full_url() =>
        Assert.Equal("Server=mysql;Port=3306;Database=pixelpay;User ID=pixelpay;Password=p@ss;",
            MySqlUrl.ToConnectionString("mysql://pixelpay:p%40ss@mysql:3306/pixelpay"));

    [Fact]
    public void Defaults_port_3306() =>
        Assert.Contains("Port=3306;", MySqlUrl.ToConnectionString("mysql://u:p@h/db"));

    [Fact]
    public void Quotes_password_with_special_characters()
    {
        var conn = MySqlUrl.ToConnectionString("mysql://u:a%3Bb%3Dc%22d'e@h:3307/db");

        Assert.Contains("Password=\"a;b=c\"\"d'e\";", conn);
        var parsed = new MySqlConnectionStringBuilder(conn);
        Assert.Equal("a;b=c\"d'e", parsed.Password);
        Assert.Equal(3307u, parsed.Port);
        Assert.Equal("db", parsed.Database);
    }

    [Fact]
    public void Rejects_non_mysql_scheme() =>
        Assert.Throws<FormatException>(() => MySqlUrl.ToConnectionString("postgres://u:p@h/db"));
}
