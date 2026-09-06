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

            // 按函数名去重：同名函数不重复注册
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
            // 缺少工具名时返回错误
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

            // 创建异步 DI 作用域，确保 KeyedService 解析在隔离的 scope 中进行
            await using var scope = serviceProvider.CreateAsyncScope();
            IToolExecutor executor;

            try
            {
                // 根据工具名从 DI 容器获取对应的 IToolExecutor 实现
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
                var guard = scope.ServiceProvider.GetService<IToolExecutionGuard>();
                if (guard is not null)
                {
                    var authorization = await guard.AuthorizeAsync(
                        toolName,
                        toolCall.Function.Arguments,
                        cancellationToken);

                    if (!authorization.Success)
                    {
                        authorization.ToolCallId ??= toolCall.Id;
                        authorization.ToolName ??= toolName;
                        return authorization;
                    }
                }

                // 执行工具调用，将结果回填 ToolCallId 和 ToolName
                var result = await executor.ExecuteAsync(toolCall.Function.Arguments, cancellationToken);
                result.ToolCallId ??= toolCall.Id;
                result.ToolName ??= toolName;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // 捕获工具执行异常，返回统一错误格式，避免单个工具异常导致整个流程崩溃；
                // 不能透传数据库、文件路径或第三方响应中的内部细节。
                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolName,
                    Success = false,
                    Content = "Tool execution failed. Please try again later.",
                    ErrorCode = "execution_error"
                };
            }
        }
    }
}
