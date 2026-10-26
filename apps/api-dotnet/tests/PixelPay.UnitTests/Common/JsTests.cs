using PixelPay.Application.Common;

namespace PixelPay.UnitTests.Common;

public class JsTests
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData("20abc", 20)]
    [InlineData(" \t\n 7", 7)]
    [InlineData("﻿8", 8)]
    [InlineData("+3", 3)]
    [InlineData("-4", -4)]
    [InlineData("1.9", 1)]
    [InlineData("1e3", 1)]
    [InlineData("0x10", 0)]
    [InlineData("007", 7)]
    [InlineData("99999999999999999999", 1e20)]
    public void ParseInt_matches_js(string s, double expected) => Assert.Equal(expected, Js.ParseInt(s));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData(" ")]
    [InlineData("٣")]
    public void ParseInt_returns_NaN_like_js(string s) => Assert.True(double.IsNaN(Js.ParseInt(s)));

    // JS gives -0, which JSON.stringify writes as 0.
    [Fact]
    public void ParseInt_negative_zero_is_plain_zero() => Assert.False(double.IsNegative(Js.ParseInt("-0")));
}
