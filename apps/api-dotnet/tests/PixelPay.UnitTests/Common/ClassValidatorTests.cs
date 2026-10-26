using System.Text.Json;
using PixelPay.Application.Common;

namespace PixelPay.UnitTests.Common;

public class ClassValidatorTests
{
    // Expected values are class-validator 0.14 / validator.js 13.15 isEmail output.
    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("A@X.COM", true)]
    [InlineData("a+b@x.io", true)]
    [InlineData("\"a b\"@x.com", true)]
    [InlineData("Ünï@x.com", true)]
    [InlineData("a@ünï.com", true)]
    [InlineData("a@x.xn--p1ai", true)]
    [InlineData("!#$%&'*+/=?^_`{|}~-@x.com", true)]
    [InlineData("test+1@pixelpay.test", true)]
    [InlineData("a@b.c", false)]
    [InlineData("a@localhost", false)]
    [InlineData("a..b@x.com", false)]
    [InlineData(".a@x.com", false)]
    [InlineData("a.@x.com", false)]
    [InlineData("a@-x.com", false)]
    [InlineData("a@x_y.com", false)]
    [InlineData("a b@x.com", false)]
    [InlineData("a@x.com.", false)]
    [InlineData("a@1.2.3.4", false)]
    [InlineData("a@[1.2.3.4]", false)]
    [InlineData("a@x.c0m", false)]
    [InlineData("a@x..com", false)]
    [InlineData("a@x.com\n", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@x.com", false)]
    public void IsEmail_matches_validator_js(string email, bool expected) =>
        Assert.Equal(expected, ClassValidator.IsEmail(email));

    [Theory]
    [InlineData("abc", 3)]
    [InlineData("\U0001F600\U0001F600aA1", 5)]
    public void Length_counts_like_validator_js(string s, int expected) =>
        Assert.Equal(expected, ClassValidator.Length(s));

    // class-validator @IsNumber() (no NaN/Infinity) and @IsPositive() on what JSON.parse gives.
    [Theory]
    [InlineData("5", true, true)]
    [InlineData("0.01", true, true)]
    [InlineData("0", true, false)]
    [InlineData("-0", true, false)]
    [InlineData("-10", true, false)]
    [InlineData("1e-400", true, false)]
    [InlineData("1e400", false, true)]
    [InlineData("-1e400", false, false)]
    [InlineData("\"5\"", false, false)]
    [InlineData("true", false, false)]
    [InlineData("null", false, false)]
    [InlineData("[1]", false, false)]
    public void IsNumber_and_IsPositive_match_class_validator(string json, bool isNumber, bool isPositive)
    {
        JsonElement? v = JsonDocument.Parse(json).RootElement;
        Assert.Equal(isNumber, ClassValidator.IsNumber(v));
        Assert.Equal(isPositive, ClassValidator.IsPositive(v));
    }

    [Fact]
    public void Missing_value_is_neither_number_nor_positive()
    {
        Assert.False(ClassValidator.IsNumber(null));
        Assert.False(ClassValidator.IsPositive(null));
    }
}
