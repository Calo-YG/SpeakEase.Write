using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AINWZ.Infrastructure.Persistence;

/// <summary>
/// 常用 JSON 值转换器工厂。
/// </summary>
internal static class JsonValueConverterFactory
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 创建列表到 JSON 的转换器。
    /// </summary>
    public static ValueConverter<List<string>, string> CreateStringListConverter()
        => new(
            value => JsonSerializer.Serialize(value ?? new List<string>(), Options),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(value, Options) ?? new List<string>());

    /// <summary>
    /// 创建字典到 JSON 的转换器。
    /// </summary>
    public static ValueConverter<Dictionary<string, string>, string> CreateStringDictionaryConverter()
        => new(
            value => JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), Options),
            value => string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(value, Options) ?? new Dictionary<string, string>());

    /// <summary>
    /// 创建列表值比较器。
    /// </summary>
    public static ValueComparer<List<string>> CreateStringListComparer()
        => new(
            (left, right) => JsonSerializer.Serialize(left ?? new List<string>(), Options) == JsonSerializer.Serialize(right ?? new List<string>(), Options),
            value => JsonSerializer.Serialize(value ?? new List<string>(), Options).GetHashCode(),
            value => value == null ? new List<string>() : value.ToList());

    /// <summary>
    /// 创建字典值比较器。
    /// </summary>
    public static ValueComparer<Dictionary<string, string>> CreateStringDictionaryComparer()
        => new(
            (left, right) => JsonSerializer.Serialize(left ?? new Dictionary<string, string>(), Options) == JsonSerializer.Serialize(right ?? new Dictionary<string, string>(), Options),
            value => JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), Options).GetHashCode(),
            value => value == null ? new Dictionary<string, string>() : value.ToDictionary(x => x.Key, x => x.Value));
}
