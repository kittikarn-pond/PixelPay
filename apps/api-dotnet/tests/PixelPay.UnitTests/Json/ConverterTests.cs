using System.Globalization;
using System.Text.Json;
using PixelPay.Api.Json;
using PixelPay.Domain.Common;

namespace PixelPay.UnitTests.Json;

public class ConverterTests
{
    [Theory]
    [InlineData("100.00", "\"100\"")] [InlineData("12.50", "\"12.5\"")]
    [InlineData("0.0000", "\"0\"")]   [InlineData("10.1234", "\"10.1234\"")]
    [InlineData("-12.50", "\"-12.5\"")]
    public void Decimal_matches_prisma(string v, string json) =>
        Assert.Equal(json, JsonSerializer.Serialize(decimal.Parse(v, CultureInfo.InvariantCulture), ApiJson.Options));

    [Fact]
    public void Negative_zero_matches_decimal_js()
    {
        Assert.Equal("\"0\"", JsonSerializer.Serialize(0m * -1m, ApiJson.Options)); // .NET clears the sign here
        Assert.Equal("\"-0\"", JsonSerializer.Serialize(new decimal(0, 0, 0, true, 2), ApiJson.Options));
    }

    [Fact]
    public void Apply_is_idempotent()
    {
        var o = new JsonSerializerOptions();
        ApiJson.Apply(o);
        ApiJson.Apply(o);
        Assert.Equal(4, o.Converters.Count);
    }

    [Fact]
    public void Date_has_three_fraction_digits_and_Z() =>
        Assert.Equal("\"2026-10-26T04:00:45.100Z\"",
            JsonSerializer.Serialize(new DateTime(2026, 10, 26, 4, 0, 45, 100, DateTimeKind.Unspecified), ApiJson.Options));

    [Fact]
    public void Date_reads_back_as_utc()
    {
        var d = JsonSerializer.Deserialize<DateTime>("\"2026-10-26T04:00:45.100Z\"", ApiJson.Options);
        Assert.Equal(DateTimeKind.Utc, d.Kind);
        Assert.Equal(new DateTime(2026, 10, 26, 4, 0, 45, 100, DateTimeKind.Utc), d);
    }

    [Fact]
    public void Raw_json_is_embedded() =>
        Assert.Equal("""{"metadata":{"a":[1,"x"]},"none":null}""",
            JsonSerializer.Serialize(new { metadata = new RawJson("""{"a":[1,"x"]}"""), none = (RawJson?)null }, ApiJson.Options));

    private enum Status { PENDING, COMPLETED }

    private sealed record Dto(string? DisplayName, Status Status, decimal? Price);

    [Fact]
    public void Camel_case_nulls_written_enums_as_names() =>
        Assert.Equal("""{"displayName":null,"status":"COMPLETED","price":null}""",
            JsonSerializer.Serialize(new Dto(null, Status.COMPLETED, null), ApiJson.Options));

    [Fact]
    public void Non_ascii_is_not_escaped() =>
        Assert.Equal("\"เติมเงิน <ok>\"", JsonSerializer.Serialize("เติมเงิน <ok>", ApiJson.Options));
}
