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

    [Theory]
    [InlineData("", 0)]
    [InlineData("  \t\n ", 0)]
    [InlineData("2", 2)]
    [InlineData(" 7 ", 7)]
    [InlineData("﻿8", 8)]
    [InlineData("+3", 3)]
    [InlineData("-4.5", -4.5)]
    [InlineData("1e1", 10)]
    [InlineData("1E-1", 0.1)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5)]
    [InlineData("007", 7)]
    [InlineData("0x10", 16)]
    [InlineData("0XfF", 255)]
    [InlineData("0o17", 15)]
    [InlineData("0b101", 5)]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("+Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData("1e999", double.PositiveInfinity)]
    public void ToNumber_matches_js(string s, double expected) => Assert.Equal(expected, Js.ToNumber(s));

    [Theory]
    [InlineData("abc")]
    [InlineData("12abc")]
    [InlineData("-0x10")]
    [InlineData("+0b1")]
    [InlineData("0x")]
    [InlineData("0b2")]
    [InlineData("0o8")]
    [InlineData("1_000")]
    [InlineData("1,5")]
    [InlineData(".")]
    [InlineData("e5")]
    [InlineData("1e")]
    [InlineData("infinity")]
    [InlineData("NaN")]
    [InlineData("٣")]
    public void ToNumber_returns_NaN_like_js(string s) => Assert.True(double.IsNaN(Js.ToNumber(s)));

    [Fact]
    public void ToNumber_negative_zero_is_plain_zero() => Assert.False(double.IsNegative(Js.ToNumber("-0")));
}
