namespace PixelPay.Domain.Common;

/// <summary>Timestamps as Prisma writes them: DATETIME(3) gets whole milliseconds, truncated (MySQL would round).</summary>
public static class PrismaTime
{
    public static DateTime Now() => Truncate(DateTime.UtcNow);

    public static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind);
}
