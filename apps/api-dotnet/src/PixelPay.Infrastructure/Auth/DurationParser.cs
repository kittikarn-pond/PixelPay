using System.Globalization;
using System.Text.RegularExpressions;

namespace PixelPay.Infrastructure.Auth;

/// <summary>Subset of the `ms` format jsonwebtoken accepts for expiresIn: "N" (seconds), "Ns", "Nm", "Nh", "Nd".</summary>
public static partial class DurationParser
{
    public static TimeSpan Parse(string v)
    {
        var m = Pattern().Match(v);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            throw new FormatException($"Invalid duration '{v}'");

        return m.Groups[2].Value switch
        {
            "m" => TimeSpan.FromMinutes(n),
            "h" => TimeSpan.FromHours(n),
            "d" => TimeSpan.FromDays(n),
            _ => TimeSpan.FromSeconds(n),
        };
    }

    [GeneratedRegex("^([0-9]+)([smhd]?)$")]
    private static partial Regex Pattern();
}
