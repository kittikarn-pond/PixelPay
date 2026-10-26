namespace PixelPay.Application.Common;

public static class Js
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

    // ECMAScript WhiteSpace + LineTerminator: .NET's set plus BOM, minus NEL (U+0085).
    private static bool IsWhiteSpace(char c) => c == '﻿' || (char.IsWhiteSpace(c) && c != '\u0085');
}
