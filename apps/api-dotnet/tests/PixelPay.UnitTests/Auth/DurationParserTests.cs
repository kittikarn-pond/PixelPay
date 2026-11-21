using PixelPay.Infrastructure.Auth;

namespace PixelPay.UnitTests.Auth;

public class DurationParserTests
{
    public static TheoryData<string, TimeSpan> Valid => new()
    {
        { "15m", TimeSpan.FromMinutes(15) },
        { "7d", TimeSpan.FromDays(7) },
        { "3600", TimeSpan.FromHours(1) },
        { "30s", TimeSpan.FromSeconds(30) },
        { "2h", TimeSpan.FromHours(2) },
        { "3650d", TimeSpan.FromDays(3650) },
    };

    [Theory]
    [MemberData(nameof(Valid))]
    public void Parses(string value, TimeSpan expected) => Assert.Equal(expected, DurationParser.Parse(value));

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("15x")]
    [InlineData("-5m")]
    [InlineData("1.5h")]
    [InlineData("99999999999999999999")]
    [InlineData("0")]
    [InlineData("0m")]
    [InlineData("999999999d")]
    public void Rejects(string value) => Assert.Throws<FormatException>(() => DurationParser.Parse(value));
}
