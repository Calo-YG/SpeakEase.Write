using System.Text.Json;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class ToolArgumentParser
{
    private readonly JsonElement _root;
    private readonly List<string> _errors = new();

    private ToolArgumentParser(JsonElement root)
    {
        _root = root;
    }

    public static ToolArgumentParser Parse(string arguments)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            return new ToolArgumentParser(doc.RootElement);
        }
        catch (JsonException ex)
        {
            var parser = new ToolArgumentParser(default);
            parser._errors.Add($"JSON 格式错误: {ex.Message}");
            return parser;
        }
    }

    public bool HasErrors => _errors.Count > 0;

    public ToolResult ToErrorResult()
    {
        return ToolResult.Fail(
            $"参数解析失败: {string.Join("; ", _errors)}",
            "argument_parse_error");
    }

    public string GetString(string name, bool required = false, string defaultValue = null)
    {
        if (_root.ValueKind == JsonValueKind.Undefined)
            return defaultValue;

        if (!_root.TryGetProperty(name, out var prop))
        {
            if (required)
                _errors.Add($"缺少必需参数 '{name}'");
            return defaultValue;
        }

        if (prop.ValueKind != JsonValueKind.String && prop.ValueKind != JsonValueKind.Null)
        {
            _errors.Add($"参数 '{name}' 类型应为 string");
            return defaultValue;
        }

        var value = prop.GetString()?.Trim();
        if (required && string.IsNullOrEmpty(value))
        {
            _errors.Add($"参数 '{name}' 不能为空");
            return defaultValue;
        }

        return value;
    }

    public int GetInt32(string name, bool required = false, int defaultValue = 0, int min = int.MinValue, int max = int.MaxValue)
    {
        if (_root.ValueKind == JsonValueKind.Undefined)
            return defaultValue;

        if (!_root.TryGetProperty(name, out var prop))
        {
            if (required)
                _errors.Add($"缺少必需参数 '{name}'");
            return defaultValue;
        }

        if (prop.ValueKind != JsonValueKind.Number)
        {
            _errors.Add($"参数 '{name}' 类型应为 integer");
            return defaultValue;
        }

        int value;
        try { value = prop.GetInt32(); }
        catch { _errors.Add($"参数 '{name}' 数值格式无效"); return defaultValue; }

        if (value < min || value > max)
        {
            _errors.Add($"参数 '{name}' 值 {value} 超出范围 [{min}, {max}]");
            return Math.Clamp(value, min, max);
        }

        return value;
    }

    public bool Has(string name)
    {
        return _root.ValueKind != JsonValueKind.Undefined && _root.TryGetProperty(name, out _);
    }

    public List<string> GetStringArray(string name, bool required = false)
    {
        if (_root.ValueKind == JsonValueKind.Undefined)
            return new List<string>();

        if (!_root.TryGetProperty(name, out var prop))
        {
            if (required)
                _errors.Add($"缺少必需参数 '{name}'");
            return new List<string>();
        }

        if (prop.ValueKind != JsonValueKind.Array)
        {
            _errors.Add($"参数 '{name}' 类型应为 array");
            return new List<string>();
        }

        var result = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            var id = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(id))
                result.Add(id);
        }
        return result;
    }

    public void Require(params string[] names)
    {
        foreach (var name in names)
        {
            if (!_root.TryGetProperty(name, out var prop) ||
                prop.ValueKind == JsonValueKind.Null ||
                (prop.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(prop.GetString())))
            {
                if (!_errors.Exists(e => e.Contains($"'{name}'")))
                    _errors.Add($"缺少必需参数 '{name}'");
            }
        }
    }
}
