using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;

namespace PixelPay.Application.Common;

/// <summary>
/// class-validator decorators for DTOs whose fields are <see cref="JsonElement"/> (Undefined when the key is absent).
/// Declare checks in the TypeScript decorator order; messages come out bottom-up per property, as Nest prints them,
/// and every failing constraint is reported (class-validator does not stop at the first one).
/// </summary>
public abstract class DtoValidator<T> : AbstractValidator<T>
{
    protected Checks Field(Expression<Func<T, JsonElement>> property, string name)
    {
        var checks = new Checks(name);
        RuleFor(property).Custom((value, ctx) =>
        {
            foreach (var message in checks.Failures(value, ctx.InstanceToValidate))
                ctx.AddFailure(name, message);
        });
        return checks;
    }

    public sealed class Checks(string name)
    {
        private readonly List<(Func<JsonElement, bool> Ok, string Message)> _checks = [];
        private Func<JsonElement, bool>? _skip;
        private Func<T, bool>? _validateIf;

        internal IEnumerable<string> Failures(JsonElement value, T dto)
        {
            if (_skip?.Invoke(value) == true || (_validateIf is not null && !_validateIf(dto)))
                return [];
            return Enumerable.Reverse(_checks).Where(c => !c.Ok(value)).Select(c => c.Message).ToList();
        }

        /// <summary><c>@IsOptional()</c>: skip all checks when the value is undefined or null.</summary>
        public Checks Optional()
        {
            _skip = v => v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
            return this;
        }

        /// <summary><c>@ValidateIf(o =&gt; !!o.x)</c>: skip all checks when the value is JS-falsy.</summary>
        public Checks IfTruthy()
        {
            _skip = v => !Truthy(v);
            return this;
        }

        public Checks When(Func<T, bool> condition)
        {
            _validateIf = condition;
            return this;
        }

        public Checks String() => Add(v => v.ValueKind == JsonValueKind.String, $"{name} must be a string");

        public Checks NotEmpty() =>
            Add(v => v.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
                     && !(v.ValueKind == JsonValueKind.String && v.GetString() == ""), $"{name} should not be empty");

        public Checks MaxLength(int max) =>
            Add(v => v.ValueKind == JsonValueKind.String && ClassValidator.Length(v.GetString()!) <= max,
                $"{name} must be shorter than or equal to {max} characters");

        public Checks MinLength(int min) =>
            Add(v => v.ValueKind == JsonValueKind.String && ClassValidator.Length(v.GetString()!) >= min,
                $"{name} must be longer than or equal to {min} characters");

        public Checks Matches(Regex regex, string message) =>
            Add(v => v.ValueKind == JsonValueKind.String && regex.IsMatch(v.GetString()!), message);

        public Checks Bool() => Add(v => v.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{name} must be a boolean value");

        public Checks Int() => Add(v => Number(v) is { } d && double.IsInteger(d), $"{name} must be an integer number");

        public Checks Number() =>
            Add(v => Number(v) is { } d && double.IsFinite(d), $"{name} must be a number conforming to the specified constraints");

        public Checks Positive() => Add(v => Number(v) > 0, $"{name} must be a positive number");

        public Checks Min(double min) =>
            Add(v => Number(v) is { } d && d >= min, $"{name} must not be less than {Format(min)}");

        public Checks Max(double max) =>
            Add(v => Number(v) is { } d && d <= max, $"{name} must not be greater than {Format(max)}");

        public Checks Uuid() =>
            Add(v => v.ValueKind == JsonValueKind.String && CvPatterns.Uuid().IsMatch(v.GetString()!), $"{name} must be a UUID");

        public Checks Url(bool requireTld = true) =>
            Add(v => v.ValueKind == JsonValueKind.String && IsUrl(v.GetString()!, requireTld), $"{name} must be a URL address");

        public Checks In(params string[] values) =>
            Add(v => v.ValueKind == JsonValueKind.String && values.Contains(v.GetString()),
                $"{name} must be one of the following values: {string.Join(", ", values)}");

        public Checks Enum<TEnum>() where TEnum : struct, System.Enum => In(System.Enum.GetNames<TEnum>());

        public Checks DateString() =>
            Add(v => v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _),
                $"{name} must be a valid ISO 8601 date string");

        private Checks Add(Func<JsonElement, bool> ok, string message)
        {
            _checks.Add((ok, message));
            return this;
        }

        private static double? Number(JsonElement v) =>
            v.ValueKind == JsonValueKind.Number
                ? double.Parse(v.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture)
                : null;

        private static string Format(double d) => d.ToString(CultureInfo.InvariantCulture);

        private static bool Truthy(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() != "",
            JsonValueKind.Number => Number(v) is { } d && d != 0 && !double.IsNaN(d),
            JsonValueKind.True or JsonValueKind.Object or JsonValueKind.Array => true,
            _ => false,
        };

        /// <summary>validator.js isURL, default options: http/https/ftp, a host, and a TLD unless disabled.</summary>
        private static bool IsUrl(string s, bool requireTld)
        {
            if (s.Length == 0 || s.Length > 2083 || s.Any(char.IsWhiteSpace))
                return false;
            var candidate = s.Contains("://", StringComparison.Ordinal) ? s : "http://" + s;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https" or "ftp")
                || string.IsNullOrEmpty(uri.Host))
                return false;
            if (!requireTld || uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
                return true;
            var labels = uri.Host.Split('.');
            return labels.Length >= 2 && CvPatterns.Tld().IsMatch(labels[^1]);
        }

    }
}

/// <summary>Reading <see cref="JsonElement"/> DTO fields; Undefined means the key was absent.</summary>
public static class JsonField
{
    public static bool Present(this JsonElement e) => e.ValueKind != JsonValueKind.Undefined;

    public static string? Str(this JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    public static bool? Bool(this JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static int? Int(this JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var i) ? i : null;

    public static decimal? Dec(this JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d) ? d : null;

    public static double? Num(this JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;

    /// <summary>Prisma update semantics: absent leaves the column alone, anything else sets it.</summary>
    public static void Set<TValue>(this JsonElement e, Func<JsonElement, TValue> read, Action<TValue> assign)
    {
        if (e.Present())
            assign(read(e));
    }
}

internal static partial class CvPatterns
{
    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    public static partial Regex Uuid();

    [GeneratedRegex(@"^([a-zA-Z¡-￿]{2,}|xn--[a-zA-Z0-9-]{2,})$")]
    public static partial Regex Tld();
}
