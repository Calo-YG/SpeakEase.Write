using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// IToolCapable 的基础实现。
    /// 管理工具定义注册与工具执行，通过 RegisterTool 同时注册定义与执行器。
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    public class ToolCapableBase(IServiceProvider serviceProvider,ILogger<IToolCapable> logger): IToolCapable
    {
        /// <summary>
        /// 工具定义注册表（工具名 → 定义），大小写不敏感。
        /// </summary>
        private readonly Dictionary<string, ToolDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IReadOnlyList<ToolDefinition> Tools => _definitions.Values.ToList().AsReadOnly();

        /// <summary>
        /// 注册一个工具，包含定义与执行器。
        /// 同名工具会覆盖已有注册。
        /// </summary>
        public void RegisterTool(ToolDefinition definition)
        {
            var name = definition.Function?.Name ?? throw new ArgumentException("工具定义的 Function.Name 不能为空。");

            if(!_definitions.TryGetValue(name, out var toolDefinition))
            {
                _definitions[name] = definition;
            }

            logger.LogInformation("Registered tool: {ToolName} (Type: {ToolType})", name, definition.Type);
        }

        /// <summary>
        /// 移除指定名称的工具注册。
        /// </summary>
        public bool UnregisterTool(string name)
        {
            return _definitions.Remove(name);
        }

        /// <inheritdoc />
        public async Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken cancellationToken = default)
        {
            if (call.Function?.Name is null)
            {
                return new ToolResult
                {
                    ToolCallId = call.Id,
                    Success = false,
                    ErrorCode = "invalid_tool_call",
                    Content = "工具调用缺少 Function.Name。"
                };
            }

            if (!_definitions.TryGetValue(call.Function.Name, out var definition))
            {
                return new ToolResult
                {
                    ToolCallId = call.Id,
                    ToolName = call.Function.Name,
                    Success = false,
                    ErrorCode = "tool_not_found",
                    Content = $"工具未注册: {call.Function.Name}"
                };
            }

            try
            {
                using var scope = serviceProvider.CreateScope();

                var executor = scope.ServiceProvider.GetRequiredKeyedService<IToolExecutor>(call.Function.Name);

                var result = await executor.ExecuteAsync(call.Function.Arguments ?? string.Empty, cancellationToken);
                result.ToolCallId ??= call.Id;
                result.ToolName ??= call.Function.Name;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ToolResult
                {
                    ToolCallId = call.Id,
                    ToolName = call.Function.Name,
                    Success = false,
                    ErrorCode = "tool_execution_failed",
                    Content = ex.Message
                };
            }
        }

        /// <summary>
        /// 获取指定名称的工具定义。
        /// </summary>
        public ToolDefinition GetToolDefinition(string name)
        {
            return _definitions.TryGetValue(name, out var def) ? def : null;
        }
    }
}
