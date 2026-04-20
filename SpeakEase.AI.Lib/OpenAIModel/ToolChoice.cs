namespace SpeakEase.AI.Lib.OpenAIModel
{
    public static class ToolChoice
    {
        public static string Auto => "auto";
        public static string None => "none";
        public static string Required => "required";
        public static object Function(string name) => new { type = "function", function = new { name } };
    }
}
