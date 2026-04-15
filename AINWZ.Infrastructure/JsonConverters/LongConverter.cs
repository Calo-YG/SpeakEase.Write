using AINWZ.Infrastructure.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINWZ.Infrastructure.JsonConverters
{
    public class LongConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();

                if (long.TryParse(str, out long value))
                {
                    return value;
                }

                // 如果解析失败，可以抛异常或者返回 null
                throw new JsonException($"无法将 \"{str}\" 转换为 DateTime");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt64();

            throw new JsonException($"不支持的长类型格式：{reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(LongToStringConverter.Convert(value));
        }
    }
}
