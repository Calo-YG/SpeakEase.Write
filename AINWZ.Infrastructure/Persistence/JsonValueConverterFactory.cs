using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.Persistence;

/// <summary>
/// 常用 JSON 值转换器工厂。
/// </summary>
internal static class JsonValueConverterFactory
{

    /// <summary>
    /// 创建列表到 JSON 的转换器。
    /// </summary>
    public static ValueConverter<List<string>, string> CreateStringListConverter()
        => new(
            value => JsonSerializer.Serialize(value ?? new List<string>(), JsonHelper.DefaultOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(value, JsonHelper.DefaultOptions) ?? new List<string>());

    /// <summary>
    /// 创建字典到 JSON 的转换器。
    /// </summary>
    public static ValueConverter<Dictionary<string, string>, string> CreateStringDictionaryConverter()
        => new(
            value => JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), JsonHelper.DefaultOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(value, JsonHelper.DefaultOptions) ?? new Dictionary<string, string>());

    /// <summary>
    /// 创建列表值比较器。
    /// </summary>
    public static ValueComparer<List<string>> CreateStringListComparer()
        => new(
            (left, right) => JsonSerializer.Serialize(left ?? new List<string>(), JsonHelper.DefaultOptions) == JsonSerializer.Serialize(right ?? new List<string>(), JsonHelper.DefaultOptions),
            value => JsonSerializer.Serialize(value ?? new List<string>(), JsonHelper.DefaultOptions).GetHashCode(),
            value => value == null ? new List<string>() : value.ToList());

    /// <summary>
    /// 创建字典值比较器。
    /// </summary>
    public static ValueComparer<Dictionary<string, string>> CreateStringDictionaryComparer()
        => new(
            (left, right) => JsonSerializer.Serialize(left ?? new Dictionary<string, string>(), JsonHelper.DefaultOptions) == JsonSerializer.Serialize(right ?? new Dictionary<string, string>(), JsonHelper.DefaultOptions),
            value => JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), JsonHelper.DefaultOptions).GetHashCode(),
            value => value == null ? new Dictionary<string, string>() : value.ToDictionary(x => x.Key, x => x.Value));
}
