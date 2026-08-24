# ADR-001: 采用模块化单体

## Status

Accepted

## Context

AINWZ 当前包含 Works、Story、World、AI 等业务，但部署、数据库和事务仍然紧密相关。当前优先问题是跨层依赖和模块边界，而不是独立扩缩容。

## Decision

保留单一 ASP.NET Core 部署，按 Auth、Works、Story、World、References、AI 划分模块，并通过项目依赖和架构测试约束模块边界。

## Trade-offs

- 接受模块共享进程和数据库的耦合。
- 获得更低的迁移成本、事务一致性和更简单的本地调试。
- 当模块出现独立扩缩容、团队 ownership 或故障隔离需求时，再评估拆分服务。
