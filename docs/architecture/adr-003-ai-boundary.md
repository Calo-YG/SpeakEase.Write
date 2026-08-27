# ADR-003: AI 运行时与业务数据访问分离

## Status

Accepted

## Implementation Status

Phase 1 completed: all AI Tools and AI infrastructure adapters now depend on the Application `IWriteDbContext` port. The concrete EF `SpeakEaseDbContext` is resolved only by Infrastructure persistence registration and health/migration code. The next phase is to replace the broad compatibility port with module-specific query/command stores.

## Context

AI Agent、Orchestrator 和 Tool 目前位于 Infrastructure，部分 Tool 直接解析 DbContext 并执行业务写入，导致模型运行时和业务规则耦合。

## Decision

SpeakEase.AI.Lib 保持 LLM 协议、Agent 模型和 Tool 框架；Application 定义 Agent/Orchestrator/Memory 合同；Infrastructure 实现 Agent、Tool 和数据库适配器。Tool 只调用 Application 端口，不直接操作 EF。

## Trade-offs

- 需要维护 AI 合同和适配器映射。
- 保留 SSE、流式 Agent 和现有工具注册键，避免一次性重写 AI 能力。
- `IWriteDbContext` remains a compatibility port; module-specific stores will replace it incrementally without changing Tool and Agent contracts.
