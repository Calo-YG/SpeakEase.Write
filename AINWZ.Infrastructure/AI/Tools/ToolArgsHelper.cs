using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public static class ToolArgsHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new TrimStringConverter() }
    };
}

public sealed class TrimStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.Trim();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
