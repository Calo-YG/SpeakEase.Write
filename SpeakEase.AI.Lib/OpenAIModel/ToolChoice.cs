namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// 工具选择策略常量。
    /// auto: LLM 自主决定是否调用工具 | none: 不调用工具 | required: 强制调用工具 | Function(name): 强制调用指定工具
    /// </summary>
    public static class ToolChoice
    {
        public static string Auto => "auto";
        public static string None => "none";
        public static string Required => "required";
        public static object Function(string name) => new { type = "function", function = new { name } };
    }
}
