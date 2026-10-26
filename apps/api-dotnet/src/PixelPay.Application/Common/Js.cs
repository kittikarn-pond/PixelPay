namespace PixelPay.Application.Common;

public static partial class Js
{
    /// <summary>JS <c>parseInt(s, 10)</c>: leading whitespace, optional sign, then ASCII digits up to the first other char.</summary>
    public static double ParseInt(string s)
    {
        var i = 0;
        while (i < s.Length && IsWhiteSpace(s[i]))
            i++;
        var negative = i < s.Length && s[i] == '-';
        if (i < s.Length && s[i] is '+' or '-')
            i++;
        var start = i;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
            i++;
        if (i == start)
            return double.NaN;
        var value = double.Parse(s.AsSpan(start, i - start), provider: System.Globalization.CultureInfo.InvariantCulture);
        // + 0.0 turns JS's -0 into 0, which is what JSON.stringify writes.
        return (negative ? -value : value) + 0.0;
    }

    /// <summary>JS <c>Number(s)</c> for a string (what unary <c>+s</c> does).</summary>
    public static double ToNumber(string s)
    {
        var t = s.AsSpan();
        while (t.Length > 0 && IsWhiteSpace(t[0]))
            t = t[1..];
        while (t.Length > 0 && IsWhiteSpace(t[^1]))
            t = t[..^1];
        if (t.IsEmpty)
            return 0;

        if (t.Length > 2 && t[0] == '0' && char.ToLowerInvariant(t[1]) is 'x' or 'o' or 'b')
        {
            var radix = char.ToLowerInvariant(t[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            double value = 0;
            foreach (var c in t[2..])
            {
                var digit = char.IsAsciiHexDigit(c) ? Convert.ToInt32(c.ToString(), 16) : radix;
                if (digit >= radix)
                    return double.NaN;
                value = value * radix + digit;
            }
            return value;
        }

        var unsigned = t.Length > 0 && t[0] is '+' or '-' ? t[1..] : t;
        if (unsigned.SequenceEqual("Infinity"))
            return t[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        if (!DecimalLiteral().IsMatch(t))
            return double.NaN;
        return double.Parse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) + 0.0;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$")]
    private static partial System.Text.RegularExpressions.Regex DecimalLiteral();

    // ECMAScript WhiteSpace + LineTerminator: .NET's set plus BOM, minus NEL (U+0085).
    private static bool IsWhiteSpace(char c) => c == '﻿' || (char.IsWhiteSpace(c) && c != '\u0085');
}
