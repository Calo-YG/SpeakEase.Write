# 分层 Prompt Composer 设计

## 目标

在不删除现有 Tool、Skill、SSE 和 `BuildPrompt()` 兼容入口的前提下，将 Agent 的身份、目标、质量标准、风格和输出契约从运行策略中分离出来。硬约束继续由 AgentLoop/Runtime Policy 负责，Prompt 只提供模型需要的软指导。

## 本轮范围

- 在 `SpeakEase.AI.Lib.Runtime` 增加 `PromptProfile`、`PromptCompositionContext` 和 `PromptComposer`。
- `INovelAgent` 增加带默认实现的 `BuildPromptProfile()`，旧 Agent/Test Double 无需立即修改。
- 现有专业 Agent 提供精简 Profile；旧 `BuildPrompt()` 保留为迁移期兼容入口。
- `CreationOrchestrator` 优先使用 Composer 生成系统提示词。
- 不修改数据库、Tool Schema、Skill 文件、SSE 字段和路由协议。

## 不在本轮范围

- 不实现真正 DAG 调度。
- 不替换 `AgentStreamChunk` 事件协议。
- 不删除 `ReActAgent` 或旧 Prompt 方法。
- 不引入第三方 Agent Framework。

## 组合规则

Prompt 由以下可选分区按固定顺序组成：身份、任务目标、质量标准、风格提示、输出契约。空分区不输出，重复换行被规范化。Runtime Policy、Tool 权限、幂等、超时和迭代预算不进入 Profile。

## 兼容策略

没有覆盖 `BuildPromptProfile()` 的旧 Agent 通过默认实现生成 legacy profile，因此可以渐进迁移。Composer 接入点位于 Orchestrator；直接调用 Agent 的场景由 `AgentBase` 使用同一 Composer 兜底。

## 验收标准

- Composer 按固定顺序输出非空分区。
- 空分区不会生成空标题。
- Prompt 中不再要求 Thought/Action/Observation 或固定工具顺序。
- 所有现有测试保持通过，Tool/Skill/SSE 行为不变。
