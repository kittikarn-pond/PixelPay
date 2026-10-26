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
}
