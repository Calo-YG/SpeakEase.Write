using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 工具参数反序列化辅助类：提供统一的 JSON 反序列化选项（snake_case 兼容 + 忽略大小写 + 字符串自动 Trim）
public static class ToolArgsHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = CreateTypeInfoResolver(),
        Converters = { new TrimStringConverter() }
    };

    private static DefaultJsonTypeInfoResolver CreateTypeInfoResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(AddSnakeCaseAliases);
        return resolver;
    }

    private static void AddSnakeCaseAliases(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties.ToArray())
        {
            if (property.Set is null)
                continue;

            var clrName = (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;
            var aliasNames = new[]
            {
                clrName,
                JsonNamingPolicy.SnakeCaseLower.ConvertName(clrName)
            };

            foreach (var aliasName in aliasNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (typeInfo.Properties.Any(candidate =>
                        string.Equals(candidate.Name, aliasName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var alias = typeInfo.CreateJsonPropertyInfo(property.PropertyType, aliasName);
                alias.Set = property.Set;
                alias.ShouldSerialize = static (_, _) => false;
                typeInfo.Properties.Add(alias);
            }
        }
    }
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
