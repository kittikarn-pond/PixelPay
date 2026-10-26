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
}
