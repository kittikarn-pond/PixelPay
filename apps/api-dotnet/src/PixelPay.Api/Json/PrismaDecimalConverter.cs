using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPay.Api.Json;

public sealed class PrismaDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("0.############################", CultureInfo.InvariantCulture));
}
