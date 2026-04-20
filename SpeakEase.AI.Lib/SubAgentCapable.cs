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

    }
}
