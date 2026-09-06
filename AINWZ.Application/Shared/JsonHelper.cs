using System.Text.Json;

namespace SpeakEase.Write.Application.Shared;

public static class JsonHelper
{
    public static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, DefaultOptions);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, DefaultOptions);
}
