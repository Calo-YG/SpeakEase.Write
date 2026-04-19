using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// ISubAgentCapable 的基础实现。
    /// 提供 SubAgent 能力：主 Agent 通过调用 spawn_subagent 工具动态创建子 Agent，
    /// 子 Agent 在独立上下文中执行任务，完成后结果摘要回传，上下文即丢弃。
    /// 
    /// 核心设计参考 nanobot SubAgent：
    /// - 复用主 Agent 的 LLM 后端（不额外创建连接）
    /// - 子 Agent 拥有独立的上下文窗口（上下文隔离）
    /// - 子 Agent 的工具集可受限（只给子任务需要的工具）
    /// - 子 Agent 的迭代次数可受限（避免失控）
    /// - 结果只回传摘要，不回传完整对话历史
    /// 
    /// 使用方式：
    /// 1. 在 ReActAgent 构造时传入 SubAgentCapable 实例
    /// 2. SubAgentCapable 自动注册 spawn_subagent 工具
    /// 3. 主 Agent 的 LLM 在 ReAct 循环中可自行决定是否调用 spawn_subagent
    /// </summary>
    public class SubAgentCapable : ISubAgentCapable
    {
        /// <summary>
        /// 默认子 Agent 最大迭代次数。
        /// </summary>
        public int DefaultMaxIterations { get; set; } = 10;

        /// <summary>
        /// 子 Agent 结果摘要的最大字符数。超过会被截断。
        /// 设为 0 或负数则不截断。
        /// </summary>
        public int MaxResultLength { get; set; } = 2000;

        /// <summary>
        /// 子 Agent 工具结果摘要的最大字符数。
        /// </summary>
        public int MaxToolSummaryLength { get; set; } = 200;

        /// <summary>
        /// 主 Agent 的 LLM 后端，子 Agent 复用此实例。
        /// </summary>
        private readonly IAgentLLMBackend _llmBackend;

        /// <summary>
        /// 子 Agent 使用的 Loop 策略工厂。默认创建 ReActLoopStrategy。
        /// </summary>
        private readonly Func<IAgentLoopStrategy> _loopStrategyFactory;

        /// <summary>
        /// 主 Agent 的工具定义列表引用，用于为子 Agent 筛选可用工具。
        /// </summary>
        private readonly Func<IReadOnlyList<ToolDefinition>> _parentToolsProvider;

        /// <summary>
        /// 主 Agent 的工具执行器引用，用于为子 Agent 转发工具调用。
        /// 参数：工具名称、工具参数 JSON、取消令牌 → 工具执行结果。
        /// </summary>
        private readonly Func<string, string, CancellationToken, Task<ToolResult>> _parentToolExecutorProvider;

        /// <summary>
        /// spawn_subagent 工具定义。
        /// </summary>
        public static readonly ToolDefinition SpawnToolDefinition = new()
        {
            Type = "function",
            Function = new ToolFunctionDefinition
            {
                Name = "spawn_subagent",
                Description = "创建一个子 Agent 来执行子任务。子 Agent 拥有独立的上下文，专注于单一任务，完成后将结果摘要返回。适用于需要上下文隔离的复杂子任务、耗时操作、或需要不同角色专注执行的场景。",
                Parameters = /*lang=json,strict*/ """
                {
                  "type": "object",
                  "properties": {
                    "task": {
                      "type": "string",
                      "description": "子 Agent 要执行的任务描述。"
                    },
                    "system_prompt": {
                      "type": "string",
                      "description": "子 Agent 的系统提示词，定义其角色和行为准则。为空时使用任务描述。"
                    },
                    "allowed_tools": {
                      "type": "array",
                      "items": { "type": "string" },
                      "description": "子 Agent 可使用的工具名称列表。为空时使用主 Agent 的全部工具。"
                    },
                    "max_iterations": {
                      "type": "integer",
                      "description": "子 Agent 最大迭代次数。为空时使用默认值 10。"
                    }
                  },
                  "required": ["task"]
                }
                """
            }
        };

        /// <summary>
        /// 构造 SubAgentCapable。
        /// </summary>
        /// <param name="llmBackend">主 Agent 的 LLM 后端，子 Agent 复用。</param>
        /// <param name="parentToolsProvider">主 Agent 的工具定义列表提供者。</param>
        /// <param name="parentToolExecutorProvider">主 Agent 的工具执行器（按名称 + 参数执行）。</param>
        /// <param name="loopStrategyFactory">子 Agent 的 Loop 策略工厂。默认创建 ReActLoopStrategy。</param>
        public SubAgentCapable(
            IAgentLLMBackend llmBackend,
            Func<IReadOnlyList<ToolDefinition>> parentToolsProvider,
            Func<string, string, CancellationToken, Task<ToolResult>> parentToolExecutorProvider,
            Func<IAgentLoopStrategy> loopStrategyFactory = null)
        {
            _llmBackend = llmBackend ?? throw new ArgumentNullException(nameof(llmBackend));
            _parentToolsProvider = parentToolsProvider ?? throw new ArgumentNullException(nameof(parentToolsProvider));
            _parentToolExecutorProvider = parentToolExecutorProvider ?? throw new ArgumentNullException(nameof(parentToolExecutorProvider));
            _loopStrategyFactory = loopStrategyFactory ?? (() => new ReActLoopStrategy());
        }

        /// <summary>
        /// 将 spawn_subagent 工具注册到指定的 ToolCapableBase。
        /// 主 Agent 构造时调用此方法，使 LLM 可以通过工具调用创建子 Agent。
        /// </summary>
        /// <param name="toolCapable">主 Agent 的工具管理器。</param>
        public void RegisterTo(ToolCapableBase toolCapable)
        {
            toolCapable.RegisterTool(SpawnToolDefinition, ExecuteSpawnToolAsync);
        }

        /// <inheritdoc />
        public async Task<SubAgentResult> SpawnAsync(
            string task,
            string systemPrompt = null,
            List<string> allowedToolNames = null,
            int? maxIterations = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(task))
            {
                return new SubAgentResult
                {
                    Success = false,
                    Error = "任务描述不能为空。"
                };
            }

            try
            {
                // 创建子 Agent
                var subAgent = CreateSubAgent(task, systemPrompt, allowedToolNames, maxIterations);

                // 构建请求
                var request = new AgentRequest
                {
                    SystemPrompt = systemPrompt ?? task,
                    Messages = new List<AgentMessage>
                    {
                        new("user", task)
                    },
                    MaxIterations = maxIterations ?? DefaultMaxIterations,
                    EnableToolDispatch = true
                };

                // 执行子 Agent
                var response = await subAgent.ChatAsync(request, cancellationToken);

                // 构建摘要结果
                return BuildResult(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new SubAgentResult
                {
                    Success = false,
                    Error = $"子 Agent 执行失败: {ex.Message}",
                    StopReason = "error"
                };
            }
        }

        /// <summary>
        /// 创建子 Agent 实例。
        /// 子 Agent 复用主 Agent 的 LLM 后端，拥有独立的工具集（受限）和 Loop 策略。
        /// </summary>
        private ChatAgentBase CreateSubAgent(
            string task,
            string systemPrompt,
            List<string> allowedToolNames,
            int? maxIterations)
        {
            var strategy = _loopStrategyFactory();
            var subAgent = new SubAgentInstance(_llmBackend, strategy);

            // 注册工具
            var parentTools = _parentToolsProvider();
            var toolsToRegister = allowedToolNames is { Count: > 0 }
                ? parentTools.Where(t => allowedToolNames.Contains(t.Function?.Name, StringComparer.OrdinalIgnoreCase)).ToList()
                : parentTools.ToList();

            foreach (var toolDef in toolsToRegister)
            {
                var capturedName = toolDef.Function?.Name; // 闭包捕获工具名
                subAgent.RegisterTool(toolDef, (args, ct) =>
                {
                    // 将子 Agent LLM 生成的参数透传给主 Agent 的工具执行器
                    return _parentToolExecutorProvider(capturedName, args, ct);
                });
            }

            return subAgent;
        }

        /// <summary>
        /// 从子 Agent 的响应构建摘要结果。
        /// 完整对话历史被丢弃，只保留最终内容 + 工具摘要。
        /// </summary>
        private SubAgentResult BuildResult(AgentResponse response)
        {
            var result = new SubAgentResult
            {
                Success = true,
                Content = Truncate(response.Content ?? string.Empty, MaxResultLength),
                Iterations = response.Iterations,
                StopReason = response.StopReason
            };

            // 构建工具调用摘要
            if (response.ToolResults is { Count: > 0 })
            {
                foreach (var tr in response.ToolResults)
                {
                    result.ToolSummaries.Add(new SubAgentToolSummary
                    {
                        ToolName = tr.ToolName,
                        Success = tr.Success,
                        ResultSummary = Truncate(tr.Content ?? string.Empty, MaxToolSummaryLength)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// spawn_subagent 工具的执行器。
        /// 解析 LLM 传入的 JSON 参数，调用 SpawnAsync，将结果序列化为工具返回值。
        /// </summary>
        private async Task<ToolResult> ExecuteSpawnToolAsync(string arguments, CancellationToken cancellationToken)
        {
            string task = null;
            string systemPrompt = null;
            List<string> allowedTools = null;
            int? maxIterations = null;

            // 解析参数
            try
            {
                var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (args is not null)
                {
                    if (args.TryGetValue("task", out var taskEl)) task = taskEl.GetString();
                    if (args.TryGetValue("system_prompt", out var spEl)) systemPrompt = spEl.GetString();
                    if (args.TryGetValue("allowed_tools", out var atEl) && atEl.ValueKind == JsonValueKind.Array)
                    {
                        allowedTools = atEl.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).ToList();
                    }
                    if (args.TryGetValue("max_iterations", out var miEl) && miEl.ValueKind == JsonValueKind.Number)
                    {
                        maxIterations = miEl.GetInt32();
                    }
                }
            }
            catch (JsonException)
            {
                // 参数解析失败，尝试将整个 arguments 作为 task
                task = arguments;
            }

            if (string.IsNullOrWhiteSpace(task))
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorCode = "invalid_arguments",
                    Content = "spawn_subagent 需要 task 参数。"
                };
            }

            // 执行子 Agent
            var result = await SpawnAsync(task, systemPrompt, allowedTools, maxIterations, cancellationToken);

            // 将 SubAgentResult 序列化为工具返回内容
            var resultContent = result.Success
                ? result.Content
                : $"子 Agent 执行失败: {result.Error}";

            return new ToolResult
            {
                Success = result.Success,
                Content = resultContent,
                ErrorCode = result.Success ? null : "subagent_failed"
            };
        }

        /// <summary>
        /// 截断字符串到指定长度，超出时添加省略号。
        /// </summary>
        private static string Truncate(string value, int maxLength)
        {
            if (maxLength <= 0 || string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }
            return value[..maxLength] + "...";
        }

        /// <summary>
        /// 内部子 Agent 实例。
        /// 最小化实现：仅组合 ChatAgentBase + IToolCapable，不支持技能和 SubAgent 递归。
        /// </summary>
        private sealed class SubAgentInstance : ChatAgentBase, IToolCapable
        {
            private readonly ToolCapableBase _toolCapable = new();

            public SubAgentInstance(IAgentLLMBackend llmBackend, IAgentLoopStrategy loopStrategy)
                : base(llmBackend, loopStrategy)
            {
            }

            public IReadOnlyList<ToolDefinition> Tools => _toolCapable.Tools;

            public Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken cancellationToken = default)
            {
                return _toolCapable.ExecuteToolAsync(call, cancellationToken);
            }

            public void RegisterTool(ToolDefinition definition, Func<string, CancellationToken, Task<ToolResult>> executor)
            {
                _toolCapable.RegisterTool(definition, executor);
            }
        }
    }
}
