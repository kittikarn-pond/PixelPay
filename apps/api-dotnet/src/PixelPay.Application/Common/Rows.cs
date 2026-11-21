using System.Collections.Concurrent;
using System.Reflection;
using PixelPay.Domain.Common;
using PixelPay.Domain.Entities;

namespace PixelPay.Application.Common;

/// <summary>
/// A Prisma-style row: every scalar column of an entity, camelCase, nulls included; navigations left out.
/// The API's JSON converters handle decimal, DateTime, enum and RawJson values.
/// </summary>
public static class Rows
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Scalars = new();

    // Prisma field names that differ from the scaffolded property names.
    private static readonly Dictionary<(Type, string), string> Renames = new()
    {
        [(typeof(Reseller), nameof(Reseller.ApprovedBy))] = "approvedById",
        [(typeof(User), nameof(User.ReferredBy))] = "referredById",
    };

    public static Dictionary<string, object?> Of(object entity, params string[] except)
    {
        var row = new Dictionary<string, object?>();
        foreach (var p in Scalars.GetOrAdd(entity.GetType(), ScalarProperties))
        {
            var name = Renames.TryGetValue((p.DeclaringType!, p.Name), out var renamed) ? renamed : CamelCase(p.Name);
            if (!except.Contains(name))
                row[name] = p.GetValue(entity);
        }
        return row;
    }

    /// <summary>Only the named columns, in the given order (a Prisma <c>select</c>).</summary>
    public static Dictionary<string, object?> Pick(object entity, params string[] names)
    {
        var all = Of(entity);
        return names.ToDictionary(n => n, n => all.TryGetValue(n, out var v) ? v : throw new ArgumentException($"No column {n}"));
    }

    public static Dictionary<string, object?> With(this Dictionary<string, object?> row, string key, object? value)
    {
        row[key] = value;
        return row;
    }

    private static PropertyInfo[] ScalarProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
            .ToArray();

    private static bool IsScalar(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(RawJson);

    private static string CamelCase(string s) => char.ToLowerInvariant(s[0]) + s[1..];
}
