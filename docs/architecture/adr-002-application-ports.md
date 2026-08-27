# ADR-002: Application 通过端口访问外部能力

## Status

Accepted

## Implementation Status

The Agent Runtime persistence slice now uses the dedicated `IAgentRuntimeDbContext` port. `IWriteDbContext` remains available for legacy application and AI modules while additional slices are migrated incrementally.

## Context

Application 当前直接引用 Infrastructure、EF DbContext、缓存、身份上下文和 ID 实现，导致业务层无法独立测试，且项目依赖方向反转。

## Decision

将当前用户、ID、缓存、AI 记忆、Token、数据库上下文等能力的接口放到 Application Abstractions；Infrastructure 引用 Application 并提供实现。复杂查询逐步替换为模块化 Store，过渡期使用 `IWriteDbContext` 作为兼容端口。

## Trade-offs

- 过渡期 `IWriteDbContext` 仍暴露 EF 类型；Agent Runtime 已先收窄为独立端口，后续按相同方式迁移 Memory、Story 和 Creation Session。
- 接口数量增加，但可以隔离实现、改善测试，并避免 Application 依赖基础设施项目。
