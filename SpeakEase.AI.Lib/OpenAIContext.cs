using SpeakEase.AI.Lib.Contract;

namespace SpeakEase.AI.Lib
{
    public sealed class OpenAIContext : IOpenAIContext
    {
        /// <summary>
        /// OpenAI API 密钥，用于认证和授权访问 OpenAI 服务。必须提供有效的 API 密钥才能调用 OpenAI 模型进行对话生成。
        /// </summary>
        public string ApiKey => throw new NotImplementedException();

        /// <summary>
        /// OpenAI API 的基础 URL，默认为 "https://api.openai.com/v1"。如果使用了代理或自定义部署的 OpenAI 模型，可以通过此属性指定不同的 URL。
        /// </summary>
        public string Url => "https://api.openai.com/v1/";

        /// <summary>
        /// OpenAI 模型名称，例如 "gpt-4"、"gpt-3.5-turbo" 等。
        /// </summary>
        public string Model => "gpt-4o-mini";
    }
}
