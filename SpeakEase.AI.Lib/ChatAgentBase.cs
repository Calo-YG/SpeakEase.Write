using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// IChatAgent 的瘦基类，实现 IAgentLoopContext。
    /// 
    /// 职责：
    /// - 持有 IAgentLLMBackend 和 IAgentLoopStrategy
    /// - 管理 IChatAgentFilter 管道
    /// - 提供 PrepareRequestAsync / ShouldExecuteTools / ExecuteToolsAsync 等虚方法供策略调用
    /// - 将 ChatAsync / StreamAsync 委托给 IAgentLoopStrategy
    /// </summary>
    public abstract class ChatAgentBase : IChatAgent, IAgentLoopContext
    {
        /// <summary>
        /// LLM 后端实例，IAgentLoopContext 的一部分，供 Loop 策略发起 LLM 调用。
        /// </summary>
        public IAgentLLMBackend LLMBackend { get; }

        /// <summary>
        /// Loop 执行策略，决定 Agent 如何迭代（如 ReAct 循环、单轮调用等）。
        /// </summary>
        private readonly IAgentLoopStrategy _loopStrategy;

        /// <summary>
        /// Filter 管道列表，ChatAsync 时按逆序聚合为中间件链。
        /// </summary>
        private readonly List<IChatAgentFilter> _filters;

        /// <summary>
        /// 构造 ChatAgentBase。
        /// </summary>
        /// <param name="llmBackend">LLM 后端，用于发起推理请求。</param>
        /// <param name="loopStrategy">Loop 执行策略，决定 Agent 的迭代行为。</param>
        /// <param name="filters">可选的 Filter 中间件列表，按传入顺序依次拦截 ChatAsync 调用。</param>
        protected ChatAgentBase(
            IAgentLLMBackend llmBackend,
            IAgentLoopStrategy loopStrategy,
            IEnumerable<IChatAgentFilter> filters = null)
        {
            LLMBackend = llmBackend ?? throw new ArgumentNullException(nameof(llmBackend));
            _loopStrategy = loopStrategy ?? throw new ArgumentNullException(nameof(loopStrategy));
            _filters = filters?.ToList() ?? new List<IChatAgentFilter>();
        }

        /// <inheritdoc />
        public virtual Task<AgentResponse> ChatAsync(AgentRequest request, CancellationToken cancellationToken = default)
        {
            // 无 Filter → 直接委托给策略
            if (_filters.Count == 0)
            {
                return _loopStrategy.ExecuteAsync(this, request, cancellationToken);
            }

            // 构建 Filter 管道
            var context = new AgentContext
            {
                AgentName = GetType().Name,
                Request = request
            };

            Task<AgentResponse> InnerNext(AgentContext ctx, CancellationToken ct)
            {
                return _loopStrategy.ExecuteAsync(this, ctx.Request, ct);
            }

            var pipeline = _filters
                .AsEnumerable()
                .Reverse()
                .Aggregate(
                    (Func<AgentContext, CancellationToken, Task<AgentResponse>>)InnerNext,
                    (next, filter) => (ctx, ct) => filter.InvokeAsync(ctx, next, ct));

            return pipeline(context, cancellationToken);
        }

        /// <inheritdoc />
        public virtual IAsyncEnumerable<AgentStreamChunk> StreamAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            // Filter 暂不支持流式拦截，直接委托给策略
            return _loopStrategy.StreamAsync(this, request, cancellationToken);
        }

        #region IAgentLoopContext 虚方法

        /// <summary>
        /// 预处理请求：注入技能系统提示词、工具定义等。
        /// 子类可重写以定制请求准备逻辑。
        /// </summary>
        public virtual Task<AgentRequest> PrepareRequestAsync(AgentRequest request, CancellationToken cancellationToken)
        {
            var prepared = CloneRequest(request);

            // 注入 IToolCapable 的工具定义（仅当请求启用工具调度时）
            if (request.EnableToolDispatch && this is IToolCapable toolCapable)
            {
                foreach (var tool in toolCapable.Tools)
                {
                    if (tool.Function?.Name is not null &&
                        !prepared.Tools.Any(t => string.Equals(t.Function?.Name, tool.Function.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        prepared.Tools.Add(CloneToolDefinition(tool));
                    }
                }
            }

            // 注入 ISkillCapable 的技能提示词
            if (this is ISkillCapable skillCapable)
            {
                if (!string.IsNullOrWhiteSpace(request.SkillName))
                {
                    var skill = skillCapable.GetSkill(request.SkillName);
                    if (skill is not null)
                    {
                        prepared.SystemPrompt = MergeSystemPrompt(skill.SystemPrompt, prepared.SystemPrompt);

                        // 注入技能的默认工具
                        if (skill.DefaultToolNames is { Count: > 0 })
                        {
                            foreach (var toolName in skill.DefaultToolNames)
                            {
                                if (!prepared.Tools.Any(t => string.Equals(t.Function?.Name, toolName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    var toolDef = (this as IToolCapable)?.Tools.FirstOrDefault(t => string.Equals(t.Function?.Name, toolName, StringComparison.OrdinalIgnoreCase));
                                    if (toolDef is not null)
                                    {
                                        prepared.Tools.Add(CloneToolDefinition(toolDef));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Task.FromResult(prepared);
        }

        /// <summary>
        /// 判断是否应执行工具调用（安全门控）。
        /// 子类可重写以添加自定义门控逻辑（如内容审查、频率限制等）。
        /// </summary>
        public virtual bool ShouldExecuteTools(AgentRequest request, AgentResponse response)
        {
            if (!request.EnableToolDispatch)
            {
                return false;
            }

            if (response.ToolCalls is null || response.ToolCalls.Count == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行工具调用列表。
        /// 默认实现通过 IToolCapable 并行执行（Task.WhenAll），子类可重写以支持串行执行等。
        /// </summary>
        public virtual async Task<List<ToolResult>> ExecuteToolsAsync(List<ToolCall> toolCalls, CancellationToken cancellationToken)
        {
            if (this is IToolCapable toolCapable)
            {
                var tasks = toolCalls.Select(async toolCall =>
                {
                    var result = await toolCapable.ExecuteToolAsync(toolCall, cancellationToken);
                    result.ToolCallId ??= toolCall.Id;
                    result.ToolName ??= toolCall.Function?.Name;
                    return result;
                });

                var results = await Task.WhenAll(tasks);
                return results.ToList();
            }

            // Agent 不支持工具 → 全部返回错误
            return toolCalls.Select(tc => new ToolResult
            {
                ToolCallId = tc.Id,
                ToolName = tc.Function?.Name,
                Success = false,
                ErrorCode = "agent_not_tool_capable",
                Content = "当前 Agent 不支持工具调用。"
            }).ToList();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 深拷贝请求对象，防止 Agent Loop 修改污染原始请求。
        /// </summary>
        private static AgentRequest CloneRequest(AgentRequest source)
        {
            return new AgentRequest
            {
                Model = source.Model,
                SystemPrompt = source.SystemPrompt,
                Messages = source.Messages.Select(CloneMessage).ToList(),
                Temperature = source.Temperature,
                MaxTokens = source.MaxTokens,
                SkillName = source.SkillName,
                MaxIterations = source.MaxIterations,
                Tools = source.Tools.Select(CloneToolDefinition).ToList(),
                EnableToolDispatch = source.EnableToolDispatch
            };
        }

        /// <summary>
        /// 深拷贝消息对象。
        /// </summary>
        private static AgentMessage CloneMessage(AgentMessage msg)
        {
            return new AgentMessage(msg.Role, msg.Content)
            {
                Name = msg.Name,
                ToolCallId = msg.ToolCallId,
                ToolCalls = msg.ToolCalls?.Select(CloneToolCall).ToList()
            };
        }

        /// <summary>
        /// 深拷贝工具定义。
        /// </summary>
        private static ToolDefinition CloneToolDefinition(ToolDefinition source)
        {
            return new ToolDefinition
            {
                Type = source.Type,
                Function = source.Function is null ? null : new ToolFunctionDefinition
                {
                    Name = source.Function.Name,
                    Description = source.Function.Description,
                    Parameters = source.Function.Parameters
                }
            };
        }

        /// <summary>
        /// 深拷贝工具调用对象。
        /// </summary>
        private static ToolCall CloneToolCall(ToolCall source)
        {
            return new ToolCall
            {
                Id = source.Id,
                Type = source.Type,
                Function = new ToolFunctionCall
                {
                    Name = source.Function?.Name ?? string.Empty,
                    Arguments = source.Function?.Arguments ?? string.Empty
                }
            };
        }

        /// <summary>
        /// 合并两段系统提示词，非空时以双换行拼接。
        /// </summary>
        private static string MergeSystemPrompt(string primary, string secondary)
        {
            if (string.IsNullOrWhiteSpace(primary)) return secondary;
            if (string.IsNullOrWhiteSpace(secondary)) return primary;
            return $"{primary}\n\n{secondary}";
        }

        #endregion
    }
}
