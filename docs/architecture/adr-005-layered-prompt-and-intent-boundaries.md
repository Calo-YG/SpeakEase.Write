# ADR-005：分层 Prompt 与意图解析边界

## 状态

Accepted

## 背景

现有 Agent Prompt 同时描述身份、业务目标、Tool 顺序、ReAct 流程、预算和输出格式。路由 Prompt 还硬编码了 Agent 典型场景和关键词规则。这使模型容易机械触发 Pipeline，也让 Prompt 承担了 Runtime 应负责的权限、幂等、超时和停止条件。

## 决策

采用两条独立边界：

1. `PromptProfile` + `PromptComposer` 负责组合身份、任务目标、用户约束、质量标准、风格提示、上下文、能力摘要和输出契约。
2. `IntentResolver` 只负责理解用户目标、候选 Agent、置信度和用户是否需要澄清；计划校验和执行仍由 Plan/AgentLoop 负责。

Runtime 策略不进入 Prompt，包括最大迭代、Tool 权限、Tool 幂等、超时、取消、恢复和副作用控制。旧 `BuildPrompt()`、`CreationRouter` 和 `AgentStreamChunk` 暂时保留为兼容入口。

## 取舍

| 方案 | 结论 | 原因 |
|---|---|---|
| 继续维护每个 Agent 的长流程 Prompt | 不采用 | 规则重复、难测试、模型行为僵化 |
| 让路由 Prompt 直接生成完整 Pipeline | 不采用 | 意图理解和执行计划耦合，容易过度编排 |
| 引入分层 Composer 和 IntentResolver | 采用 | 可测试、可渐进迁移，并保留现有 Tool/Skill/SSE |

## 后果

- 新 Agent 只需要声明 Profile，不需要复制 Runtime 流程脚本。
- Prompt 可以按请求动态注入约束和上下文。
- 旧 Agent 仍可通过 `BuildPrompt()` 运行，迁移期间存在双入口。
- 后续需要补充 `PlanCompiler`，将 `IntentResolution` 编译为真正受约束的 DAG。

## 验收

- Composer 对空分区不生成空标题，且顺序稳定。
- 专业 Agent 的主编排 Prompt 不包含 ReAct/Thought/固定工具顺序。
- 路由 Prompt 使用注册 Agent 描述，不再包含业务关键词清单。
- Tool、Skill、SSE 兼容测试保持通过。
