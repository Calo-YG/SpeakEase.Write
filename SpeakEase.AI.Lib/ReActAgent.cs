using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// ReActAgent 实现了基于 ReAct 策略的 Agent，集成了对话能力、工具调用能力和技能调用能力。
    /// </summary>
    /// <param name="chatCompatible">用于处理对话的 LLM 后端实例。</param>
    /// <param name="toolCapable">用于执行工具调用的能力实例。</param>
    /// <param name="skillCapable">用于管理和调用技能的能力实例。</param>
    /// <param name="filters">可选的 Filter 中间件列表，按传入顺序依次拦截 ChatAsync 调用。</param>
    public class ReActAgent(IChatCompatible chatCompatible, IToolCapable toolCapable, ISkillCapable skillCapable, IEnumerable<IChatAgentFilter> filters = null) : IToolCapable, ISkillCapable
    {
        /// <summary>
        /// tools 列表，供 Agent 策略在准备请求时注入到提示词中，或供 Filter/策略在运行时查询工具定义。
        /// </summary>
        public IReadOnlyList<ToolDefinition> Tools => toolCapable.Tools;

        /// <summary>
        /// skills 列表，供 Agent 策略在准备请求时注入到提示词中，或供 Filter/策略在运行时查询技能定义。
        /// </summary>
        public IReadOnlyList<SkillDefinition> Skills => skillCapable.Skills;


        /// <summary>
        /// 工具执行
        /// </summary>
        /// <param name="call"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken cancellationToken = default)
        {
           return toolCapable.ExecuteToolAsync(call, cancellationToken);
        }

        /// <summary>
        /// 获取所有Skills
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public SkillDefinition GetSkill(string name)
        {
            return skillCapable.GetSkill(name);
        }

        /// <summary>
        /// 注册skills
        /// </summary>
        /// <param name="skill"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void RegisterSkill(SkillDefinition skill)
        {
            skillCapable.RegisterSkill(skill);
        }

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="tool"></param>
        public void RegisterTool(ToolDefinition tool)
        {
            toolCapable.RegisterTool(tool);
        }
    }
}
