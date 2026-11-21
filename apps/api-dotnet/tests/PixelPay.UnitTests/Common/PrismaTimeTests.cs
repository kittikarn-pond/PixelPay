using PixelPay.Domain.Common;

namespace PixelPay.UnitTests.Common;

public class PrismaTimeTests
{
    [Fact]
    public void Truncate_drops_sub_millisecond_ticks_without_rounding()
    {
        var value = new DateTime(2026, 10, 26, 12, 0, 0, 999, DateTimeKind.Utc).AddTicks(9999);

        var truncated = PrismaTime.Truncate(value);

        Assert.Equal(new DateTime(2026, 10, 26, 12, 0, 0, 999, DateTimeKind.Utc), truncated);
        Assert.Equal(DateTimeKind.Utc, truncated.Kind);
    }

    [Fact]
    public void Now_is_whole_milliseconds_utc()
    {
        var now = PrismaTime.Now();

        Assert.Equal(0, now.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal(DateTimeKind.Utc, now.Kind);
    }
}
