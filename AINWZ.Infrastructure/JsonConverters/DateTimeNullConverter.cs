using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.Write.Infrastructure.JsonConverters
{
    public class DateTimeNullConverter: JsonConverter<DateTime?>
    {
        private const string Format = "yyyy-MM-dd HH:mm:ss";

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                    return null;

                if (DateTime.TryParse(str, out var dt))
                    return dt;

                // 如果解析失败，可以抛异常或者返回 null
                throw new JsonException($"无法将 \"{str}\" 转换为 DateTime");
            }

            if (reader.TokenType == JsonTokenType.Null)
                return null;

            // 数字时间戳（Unix 时间戳毫秒）
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var timestamp))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
            }

            throw new JsonException($"不支持的时间格式：{reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString(Format));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
