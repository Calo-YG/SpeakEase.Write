using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// ReAct 模式 Agent 的具体实现。
    /// 组合 ChatAgentBase + ReActLoopStrategy + ToolCapableBase + SkillCapableBase + SubAgentCapable。
    /// 
    /// 使用示例：
    /// <code>
    /// var agent = new ReActAgent(llmBackend);
    /// agent.RegisterTool(echoToolDef, (args, ct) => ...);
    /// agent.RegisterSkill(new SkillDefinition { Name = "writer", SystemPrompt = "你是小说家", DefaultToolNames = [...] });
    /// agent.EnableSubAgent();  // 启用 SubAgent，LLM 可调用 spawn_subagent 工具
    /// var response = await agent.ChatAsync(new AgentRequest { Messages = [...] });
    /// </code>
    /// </summary>
    public class ReActAgent : ChatAgentBase, IToolCapable, ISkillCapable, ISubAgentCapable
    {
        private readonly ToolCapableBase _toolCapable;
        private readonly SkillCapableBase _skillCapable;
        private SubAgentCapable _subAgentCapable;

        /// <summary>
        /// 使用默认 ReActLoopStrategy 构造。
        /// </summary>
        public ReActAgent(IAgentLLMBackend llmBackend, IEnumerable<IChatAgentFilter> filters = null)
            : base(llmBackend, new ReActLoopStrategy(), filters)
        {
            _toolCapable = new ToolCapableBase();
            _skillCapable = new SkillCapableBase();
        }

        /// <summary>
        /// 使用自定义 Loop 策略构造。
        /// </summary>
        public ReActAgent(IAgentLLMBackend llmBackend, IAgentLoopStrategy loopStrategy, IEnumerable<IChatAgentFilter> filters = null)
            : base(llmBackend, loopStrategy, filters)
        {
            _toolCapable = new ToolCapableBase();
            _skillCapable = new SkillCapableBase();
        }

        #region IToolCapable - 委托给 ToolCapableBase

        /// <inheritdoc />
        public IReadOnlyList<ToolDefinition> Tools => _toolCapable.Tools;

        /// <inheritdoc />
        public Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken cancellationToken = default)
        {
            return _toolCapable.ExecuteToolAsync(call, cancellationToken);
        }

        /// <summary>
        /// 注册一个工具（定义 + 执行器）。
        /// </summary>
        public void RegisterTool(ToolDefinition definition, Func<string, CancellationToken, Task<ToolResult>> executor)
        {
            _toolCapable.RegisterTool(definition, executor);
        }

        /// <summary>
        /// 移除指定名称的工具注册。
        /// </summary>
        public bool UnregisterTool(string name)
        {
            return _toolCapable.UnregisterTool(name);
        }

        /// <summary>
        /// 获取指定名称的工具定义。
        /// </summary>
        public ToolDefinition GetToolDefinition(string name)
        {
            return _toolCapable.GetToolDefinition(name);
        }

        #endregion

        #region ISubAgentCapable - 委托给 SubAgentCapable

        /// <summary>
        /// 启用 SubAgent 能力。
        /// 调用后，spawn_subagent 工具将自动注册到 Agent 的工具列表中，
        /// LLM 可在 ReAct 循环中自行决定是否创建子 Agent 来执行子任务。
        /// </summary>
        /// <param name="loopStrategyFactory">子 Agent 的 Loop 策略工厂。默认创建 ReActLoopStrategy。</param>
        /// <returns>this，支持链式调用。</returns>
        public ReActAgent EnableSubAgent(Func<IAgentLoopStrategy> loopStrategyFactory = null)
        {
            _subAgentCapable = new SubAgentCapable(
                LLMBackend,
                () => _toolCapable.Tools,
                (toolName, args, ct) =>
                {
                    // 通过 ToolCapableBase 执行工具，透传子 Agent LLM 生成的参数
                    var call = new ToolCall
                    {
                        Id = Guid.NewGuid().ToString("N")[..8],
                        Type = "function",
                        Function = new ToolFunctionCall { Name = toolName, Arguments = args }
                    };
                    return _toolCapable.ExecuteToolAsync(call, ct);
                },
                loopStrategyFactory);

            _subAgentCapable.RegisterTo(_toolCapable);
            return this;
        }

        /// <inheritdoc />
        public Task<SubAgentResult> SpawnAsync(string task, string systemPrompt = null, List<string> allowedToolNames = null, int? maxIterations = null, CancellationToken cancellationToken = default)
        {
            if (_subAgentCapable is null)
            {
                throw new InvalidOperationException("SubAgent 能力未启用。请先调用 EnableSubAgent()。");
            }
            return _subAgentCapable.SpawnAsync(task, systemPrompt, allowedToolNames, maxIterations, cancellationToken);
        }

        #endregion

        #region ISkillCapable - 委托给 SkillCapableBase

        /// <inheritdoc />
        public IReadOnlyList<SkillDefinition> Skills => _skillCapable.Skills;

        /// <inheritdoc />
        public void RegisterSkill(SkillDefinition skill)
        {
            _skillCapable.RegisterSkill(skill);
        }

        /// <inheritdoc />
        public SkillDefinition GetSkill(string name)
        {
            return _skillCapable.GetSkill(name);
        }

        /// <summary>
        /// 移除指定名称的技能注册。
        /// </summary>
        public bool UnregisterSkill(string name)
        {
            return _skillCapable.UnregisterSkill(name);
        }

        #endregion
    }
}
