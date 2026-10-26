using System.Text.Json;
using System.Text.Json.Serialization;
using PixelPay.Domain.Common;

namespace PixelPay.Api.Json;

public sealed class RawJsonConverter : JsonConverter<RawJson>
{
    public override RawJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return new RawJson(doc.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, RawJson value, JsonSerializerOptions options)
    {
        if (value.Value is null)
            writer.WriteNullValue();
        else
            writer.WriteRawValue(value.Value);
    }
}
