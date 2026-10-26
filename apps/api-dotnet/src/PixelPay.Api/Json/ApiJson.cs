using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPay.Api.Json;

public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Apply(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.DictionaryKeyPolicy = null;
        o.PropertyNameCaseInsensitive = false;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.NumberHandling = JsonNumberHandling.Strict;
        // JSON.stringify does not escape non-ASCII or HTML characters.
        o.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        AddOnce(o, new PrismaDecimalConverter());
        AddOnce(o, new UtcDateTimeConverter());
        AddOnce(o, new RawJsonConverter());
        AddOnce(o, new JsonStringEnumConverter());
    }

    private static void AddOnce(JsonSerializerOptions o, JsonConverter converter)
    {
        if (!o.Converters.Any(c => c.GetType() == converter.GetType()))
            o.Converters.Add(converter);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Apply(o);
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
