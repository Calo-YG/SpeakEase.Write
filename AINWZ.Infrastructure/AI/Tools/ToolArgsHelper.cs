using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 工具参数反序列化辅助类：提供统一的 JSON 反序列化选项（忽略大小写 + 字符串自动 Trim）
public static class ToolArgsHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new TrimStringConverter() }
    };
}

// 字符串 Trim 转换器：JSON 反序列化时自动去除字符串首尾空白
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
