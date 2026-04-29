using System.Text.Json;

namespace SpeakEase.Write.Infrastructure.Shared;

/// <summary>
/// 统一 JSON 序列化工具。
/// 所有项目代码统一通过此入口进行 JSON 操作，避免各处散落重复的 JsonSerializerOptions 实例。
/// </summary>
public static class JsonHelper
{
    public static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 序列化为 JSON 字符串
    /// </summary>
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, DefaultOptions);
    }

    /// <summary>
    /// 反序列化 JSON 字符串
    /// </summary>
    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }
}
