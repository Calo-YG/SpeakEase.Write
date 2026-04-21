using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using Microsoft.Extensions.DependencyInjection;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 工具能力实现：维护工具定义列表，通过 DI KeyedService 按名路由执行
    /// </summary>
    public sealed class ToolCapable(IServiceProvider serviceProvider) : IToolCapable
    {
        private readonly List<ToolDefinition> _tools = [];

        /// <inheritdoc />
        public IReadOnlyList<ToolDefinition> Tools => _tools;

        /// <inheritdoc />
        public void RegisterTool(ToolDefinition tool)
        {
            ArgumentNullException.ThrowIfNull(tool);

            // 按函数名去重，避免重复注册
            if (tool.Function?.Name is not null &&
                _tools.Any(t => t.Function?.Name == tool.Function.Name))
                return;

            _tools.Add(tool);
        }

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(toolCall);

            var toolName = toolCall.Function?.Name;
            if (string.IsNullOrEmpty(toolName))
            {
                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolName,
                    Success = false,
                    Content = "Tool call is missing function name.",
                    ErrorCode = "missing_function_name"
                };
            }

            await using var scope = serviceProvider.CreateAsyncScope();
            IToolExecutor executor;

            try
            {
                executor = scope.ServiceProvider.GetRequiredKeyedService<IToolExecutor>(toolName);
            }
            catch (InvalidOperationException)
            {
                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolName,
                    Success = false,
                    Content = $"No executor registered for tool '{toolName}'.",
                    ErrorCode = "executor_not_found"
                };
            }

            try
            {
                var result = await executor.ExecuteAsync(toolCall.Function.Arguments, cancellationToken);
                result.ToolCallId ??= toolCall.Id;
                result.ToolName ??= toolName;
                return result;
            }
            catch (Exception ex)
            {
                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolName,
                    Success = false,
                    Content = $"Tool execution failed: {ex.Message}",
                    ErrorCode = "execution_error"
                };
            }
        }
    }
}
