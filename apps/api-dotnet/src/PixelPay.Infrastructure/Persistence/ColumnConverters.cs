using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PixelPay.Domain.Common;

namespace PixelPay.Infrastructure.Persistence;

/// <summary>Prisma stores DateTime(3) as UTC with no kind: read back as Utc, write as-is.</summary>
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(v => ForWrite(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
{
    private static DateTime ForWrite(DateTime v) => v.Kind == DateTimeKind.Local
        ? throw new InvalidOperationException("Local DateTime cannot be written; use UTC.")
        : v;
}

public sealed class RawJsonColumnConverter()
    : ValueConverter<RawJson, string>(v => v.Value, v => new RawJson(v));
