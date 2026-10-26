using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PixelPay.Application.Common;

/// <summary>
/// class-validator checks for DTO fields bound as <see cref="JsonElement"/>, so a wrong JSON type reaches the
/// validator (and gives "x must be a string") instead of failing deserialization. Ports of validator.js 13.15.
/// </summary>
public static partial class ClassValidator
{
    /// <summary>The value if it is a JSON string, else null.</summary>
    public static string? Text(this JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static bool IsString(JsonElement? value) => value is { ValueKind: JsonValueKind.String };

    /// <summary>JSON undefined or null, which <c>@IsOptional()</c> skips.</summary>
    public static bool IsMissing(JsonElement? value) => value is null or { ValueKind: JsonValueKind.Null };

    /// <summary><c>@IsNumber()</c>: a JS number that is not NaN or ±Infinity (JSON.parse turns 1e400 into Infinity).</summary>
    public static bool IsNumber(JsonElement? value) => JsNumber(value) is { } d && double.IsFinite(d);

    /// <summary><c>@IsPositive()</c>: a JS number greater than zero, Infinity included.</summary>
    public static bool IsPositive(JsonElement? value) => JsNumber(value) > 0;

    /// <summary>The number JSON.parse would give, or null for a non-number.</summary>
    private static double? JsNumber(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } v
            ? double.Parse(v.GetRawText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)
            : null;

    public static bool MinLength(JsonElement? value, int min) => value.Text() is { } s && Length(s) >= min;

    public static bool MaxLength(JsonElement? value, int max) => value.Text() is { } s && Length(s) <= max;

    public static bool IsEmail(JsonElement? value) => value.Text() is { } s && IsEmail(s);

    /// <summary>validator.js isLength: UTF-16 units minus surrogate pairs and variation-selector sequences.</summary>
    public static int Length(string s) =>
        s.Length - PresentationSequence().Count(s) - SurrogatePair().Count(s);

    /// <summary>validator.js isEmail with class-validator's default options.</summary>
    public static bool IsEmail(string s)
    {
        if (s.Length > 254)
            return false;
        var at = s.LastIndexOf('@');
        var domain = s[(at + 1)..];
        var user = at < 0 ? "" : s[..at];
        if (Encoding.UTF8.GetByteCount(user) > 64 || Encoding.UTF8.GetByteCount(domain) > 254 || !IsFqdn(domain))
            return false;
        if (user.Length > 0 && user[0] == '"' && user[^1] == '"')
            return QuotedUser().IsMatch(user.Length > 1 ? user[1..^1] : "");
        return user.Split('.').All(part => UserPart().IsMatch(part));
    }

    private static bool IsFqdn(string s)
    {
        var parts = s.Split('.');
        var tld = parts[^1];
        if (parts.Length < 2 || !Tld().IsMatch(tld) || tld.Any(char.IsWhiteSpace) || NumericTld().IsMatch(tld))
            return false;
        return parts.All(part => part.Length <= 63
            && DomainLabel().IsMatch(part)
            && !FullWidth().IsMatch(part)
            && part[0] != '-' && part[^1] != '-'
            && !part.Contains('_'));
    }

    [GeneratedRegex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]")]
    private static partial Regex SurrogatePair();

    [GeneratedRegex(@"[^\uFE0F\uFE0E][\uFE0F\uFE0E]")]
    private static partial Regex PresentationSequence();

    [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+\-/=?^_`{|}~\u00A1-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]+\z")]
    private static partial Regex UserPart();

    [GeneratedRegex(@"^([\s\x01-\x08\x0b\x0c\x0e-\x1f\x7f\x21\x23-\x5b\x5d-\x7e\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]|(\\[\x01-\x09\x0b\x0c\x0d-\x7f\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))*\z")]
    private static partial Regex QuotedUser();

    [GeneratedRegex(@"^([A-Za-z\u00A1-\u00A8\u00AA-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]{2,}|[xX][nN][A-Za-z0-9-]{2,})\z")]
    private static partial Regex Tld();

    [GeneratedRegex(@"^[0-9]+\z")]
    private static partial Regex NumericTld();

    [GeneratedRegex(@"^[A-Za-z_\u00A1-\uFFFF0-9-]+\z")]
    private static partial Regex DomainLabel();

    [GeneratedRegex(@"[\uFF01-\uFF5E]")]
    private static partial Regex FullWidth();
}
