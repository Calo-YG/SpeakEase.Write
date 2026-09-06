# AINWZ Architecture Refactor Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 AINWZ 从“按项目分层但跨层直接访问”的结构，渐进式重构为按业务模块组织的模块化单体，同时保持 API、数据库和 AI 创作能力可持续演进。

**Architecture:** 保留 ASP.NET Core + PostgreSQL + EF Core + SSE 的单体部署形态，建立 `API → Application → Domain` 的单向依赖，`Infrastructure` 只实现 Application/Domain 定义的端口。业务模块按 Auth、Works、Story、World、References、AI 切分；暂不引入微服务、完整 CQRS 或事件溯源。

**Tech Stack:** .NET 10、ASP.NET Core Minimal API、EF Core/Npgsql、Serilog、xUnit、Moq、SpeakEase.AI.Lib。

---

## 现状基线与约束

- 当前为六个项目：`AINWZ`、`AINWZ.Application`、`AINWZ.Domain`、`AINWZ.Infrastructure`、`SpeakEase.AI.Lib`、`AINWZ.Tests`。
- `AINWZ.Application` 直接引用 `AINWZ.Infrastructure`；几乎所有 Application 服务直接注入 `SpeakEaseDbContext`、`ISnowflakeIdGenerator`、`IUserContext` 和 `Infrastructure.Shared`。
- `AINWZ.Infrastructure/AI/Tools` 中约 49 个工具直接解析 `SpeakEaseDbContext`，AI 适配器承载了业务写入逻辑。
- 仓储接口物理位置在 `AINWZ.Domain/Repositories`，但多个命名空间是 `SpeakEase.Write.Application.Repositories`；当前 Application 服务也没有使用这些仓储。
- 最大服务是 `WorldApplication.cs`（659 行）、`UserModelConfigApplication.cs`（598 行）、`CreationSessionManager.cs`（518 行），同时承担授权、查询、事务、映射和日志。
- 当前测试只有 13 个 `[Fact]/[Theory]`，主要覆盖 AI/会话链路。
- 基线 `dotnet test --no-restore` 当前未通过：`AINWZ.Tests/AI/AgentApplicationTests.cs:31` 将 `ServiceProvider` 传给了现在要求 `IServiceScopeFactory` 的 `CreationRouter` 构造函数。该问题来自工作区已有未提交修改，应先单独修复或回滚。另有 `Microsoft.OpenApi 2.0.0` 的 NU1903 高危依赖告警，以及关闭 nullable 后仍出现的 CS8632 告警。

## 目标依赖关系

```text
AINWZ (API/Composition Root)
  ├── AINWZ.Application
  ├── AINWZ.Infrastructure
  └── SpeakEase.AI.Lib

AINWZ.Application ──> AINWZ.Domain (+ stable SpeakEase.AI.Lib contracts)
AINWZ.Infrastructure ──> AINWZ.Application + AINWZ.Domain + SpeakEase.AI.Lib
AINWZ.Domain ──> 无业务项目
```

Application 不再引用 Infrastructure；API 作为唯一组合根显式引用 Infrastructure。结果模型、分页、当前用户、ID、时间、AI 编排等依赖均通过 Application 抽象进入。

## 目标目录结构

```text
AINWZ.Application/
  Abstractions/{Identity,Persistence,Time,Ids,Modules}/
  Modules/{Auth,Works,Story,World,References,AI}/
  Shared/{Results,Pagination,Serialization,Validation}/
AINWZ.Domain/
  Entities/{AI,Learning,Memory,Story,Tags,Users,Works,World}/
  Repositories/{Auth,Works,Story,World,AI}/
  ValueObjects/
AINWZ.Infrastructure/
  Persistence/{Configurations,Migrations,Stores,Interceptors}/
  Identity/ Ids/ Caching/
  AI/{Adapters,Agents,Tools,Memory,Orchestration,MessageBus}/
AINWZ/
  MapRoute/{Auth,Works,Story,World,References,AI}/
  Middleware/ HealthChecks/
```

## 分阶段实施计划

### Task 0: 固化基线和边界规则

**Files:**
- Create: `docs/architecture/adr-001-modular-monolith.md`
- Create: `docs/architecture/adr-002-application-ports.md`
- Create: `docs/architecture/adr-003-ai-boundary.md`
- Create: `AINWZ.Tests/Architecture/DependencyRulesTests.cs`
- Modify: `Directory.Build.props`（若需要统一警告策略）

- [ ] 记录 `git status --short --branch`、`dotnet build SpeakEase.Write.slnx --no-restore`、`dotnet test --no-restore` 的结果。
- [ ] 架构测试读取各 `.csproj`，断言 Application 不引用 Infrastructure、Domain 不引用业务项目、Route 不引用 Infrastructure 类型。
- [ ] ADR 记录模块化单体、模块 Store/Repository 端口、AI 运行时边界的候选方案、取舍和回退条件。
- [ ] 运行 `dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~DependencyRulesTests`。

### Task 1: 纠正项目依赖和共享内核

**Files:**
- Modify: `AINWZ.Application/SpeakEase.Write.Application.csproj`
- Modify: `AINWZ.Infrastructure/SpeakEase.Write.Infrastructure.csproj`
- Modify: `AINWZ/SpeakEase.Write.csproj`
- Move/Create: `AINWZ.Application/Shared/{Results,Pagination,Serialization}/*.cs`
- Create: `AINWZ.Application/Abstractions/{Identity/IUserContext.cs,Ids/IIdGenerator.cs,Time/IClock.cs}`
- Modify: all `AINWZ.Application/**/*.cs` using `SpeakEase.Write.Infrastructure.*`

- [ ] 将 `ApiResult`、`PageResult`、`Pagination`、`JsonHelper` 迁入 Application.Shared，删除 Application 对 Infrastructure 的项目引用。
- [ ] 将 `IUserContext`、`ISnowflakeIdGenerator` 接口移入 Application Abstractions；Infrastructure 只保留实现，新增可测试的 `IClock/TimeProvider`。
- [ ] 将 `AINWZ.Domain/Repositories/*.cs` 的命名空间统一为 `SpeakEase.Write.Domain.Repositories`，修正注册引用；审计后删除无调用方的 Generic Repository。
- [ ] 让 Infrastructure 反向引用 Application；API 显式引用 Infrastructure，并承担所有 DI 注册。
- [ ] 运行 `dotnet build SpeakEase.Write.slnx --no-restore` 和架构测试，确认 Application 无 Infrastructure using。

### Task 2: 按业务模块拆分 Application 服务

**Files:**
- Create: `AINWZ.Application/Modules/Works/{Commands,Queries}/Work*Service.cs`
- Create: `AINWZ.Application/Modules/World/{Commands,Queries}/World*Service.cs`
- Create: `AINWZ.Application/Modules/AI/{Commands,Queries}/CreationSession*Service.cs`
- Modify: `AINWZ.Application/Applications/{Work,World,UserModelConfig,CreationSessionManager}*.cs`
- Modify: `AINWZ.Application/ServiceCollectionExtensions.cs`

- [ ] 以 `WorkApplication` 为试点，将查询和命令拆开；原 `IWorkApplication` 暂时作为 Facade，保持 Route 和 JSON 不变。
- [ ] 提取 `IWorkAccessChecker` 和模块 Mapping，统一所有权校验、404 语义和 DTO 投影。
- [ ] 拆分 `WorldApplication`、`UserModelConfigApplication`、`CreationSessionManager`；目标每个类小于 250 行，状态机和事务策略保持在同一模块。
- [ ] 每迁移一个模块就补充未登录、越权、成功、重复操作、事务回滚测试；不要一次性修改全部 25 个 Application 服务。

### Task 3: 建立模块化数据访问层

**Files:**
- Create: `AINWZ.Application/Abstractions/Modules/{Works,Story,World,AI}/*Store.cs`
- Create: `AINWZ.Infrastructure/Persistence/Stores/{Works,Story,World,AI}/Ef*Store.cs`
- Modify: `AINWZ.Infrastructure/Persistence/SpeakEaseDbContext.cs`
- Modify: `AINWZ.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
- Modify: all Application services currently injecting `SpeakEaseDbContext`

- [ ] Store 返回 Application 定义的投影或模块 DTO，不暴露 `IQueryable`、EF entity 或 `DbSet`；只读查询固定 `AsNoTracking()`，分页先查 ID 再查详情。
- [ ] 写入端口提供明确的聚合语义，跨表写入通过 `IUnitOfWork`；`ExecuteDeleteAsync/ExecuteUpdateAsync` 保留在 Infrastructure Store。
- [ ] 先迁移 Works 删除级联、CreationSession 追加/归档/回滚，再迁移 Story、World、References、Users；每次只改一个模块。
- [ ] 删除 Application 对 EF Core 的直接依赖后，再清理未使用的 `EfAggregateRootRepository<T>` 和重复注册。
- [ ] 使用 SQLite/PostgreSQL 集成测试验证事务和批量更新，不把 InMemory 作为唯一事务证明。

### Task 4: 重构 AI 边界和工具执行

**Files:**
- Create: `AINWZ.Application/Modules/AI/{Contracts,Abstractions}/*.cs`
- Modify: `AINWZ.Infrastructure/AI/{Orchestrator,Agents,Tools}/*.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs`

- [ ] 把 `INovelAgent`、路由结果、上下文和编排器合同移到 Application AI；`SpeakEase.AI.Lib` 只提供 LLM 协议、Agent 模型和 Tool 框架。
- [ ] `CreationOrchestrator` 只依赖 AI context/command/query 端口、LLM 合同和 Agent，不直接依赖 DbContext 或 Infrastructure.Shared。
- [ ] `Get*Tool` 只调用查询端口，`Create*/Update*/Save*Tool` 只调用命令端口；工具不负责 EF、事务、主键生成。
- [ ] 定义稳定的 stream error code、可重试标记、取消语义，禁止将 `Exception.Message` 原样暴露给客户端。
- [ ] 修复当前 `AgentApplicationTests` 构造函数错误后，覆盖单 Agent、pipeline、路由回退、共享上下文只构建一次、取消和工具失败。

### Task 5: 整理 API 端点和组合根

**Files:**
- Modify: `AINWZ/Program.cs`
- Create: `AINWZ/MapRoute/EndpointRegistration.cs`
- Move/Modify: `AINWZ/MapRoute/{Auth,Works,Story,World,References,AI}/*.cs`
- Modify: `AINWZ/HealthChecks/DbContextHealthCheck.cs`
- Modify: `AINWZ/Middleware/GlobalExceptionMiddleware.cs`

- [ ] 将日志、JSON、CORS、健康检查、中间件、端点注册收敛为扩展，`Program.cs` 只保留组合根。
- [ ] 新增 `MapAllEndpoints`，Route 只依赖 Application.Contracts，不依赖 Infrastructure 类型或 EF entity。
- [ ] 统一 `api/{module}` 前缀并保留旧路径一个版本周期；除注册/登录/刷新令牌外，组默认 `.RequireAuthorization()`；SSE 明确 `text/event-stream`、取消 token 和禁缓存。
- [ ] 统一 ProblemDetails/HTTP 状态码/OpenAPI 标签与响应类型；添加匿名、越权、分页、SSE 断开和异常映射的 WebApplicationFactory 测试。

### Task 6: 领域、数据库和横切能力硬化

**Files:**
- Modify: `AINWZ.Domain/Entities/**/*.cs`
- Create: `AINWZ.Domain/ValueObjects/{WorkStatus,SessionStatus}.cs`
- Create: `AINWZ.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/**/*.cs`
- Modify: `AINWZ.Infrastructure/JsonConverters/*.cs`

- [ ] 用 UTC + `IClock/TimeProvider` 替代 Application 内 `DateTime.Now`，用 SaveChanges interceptor 统一审计字段。
- [ ] 先以常量/值对象和 EF converter 收敛 Work、Session、Message role 的字符串状态，保持数据库兼容后再考虑数据库枚举。
- [ ] 升级或锁定引入 `Microsoft.OpenApi 2.0.0` 的包，运行 `dotnet list package --vulnerable`；清理 nullable 告警。
- [ ] 审查 `WorkId/UserId/SessionId` 索引、唯一键、级联删除和手动删除逻辑，增加孤儿数据检查。

### Task 7: 测试、可观测性和发布

**Files:**
- Create: `AINWZ.Tests/Architecture/DependencyRulesTests.cs`
- Create: `AINWZ.Tests/Modules/{Works,Story,World,AI}/*Tests.cs`
- Modify: `AINWZ.Tests/AI/*.cs`
- Create: `docs/architecture/refactor-runbook.md`

- [ ] 建立测试金字塔：Application 纯单测、Store 数据库集成测、API 少量端到端测、AI 固定假 LLM；默认测试禁止真实网络。
- [ ] 记录 HTTP、LLM、Agent step、tool、事务耗时和成功率，关联 `workId/sessionId`，不记录 ApiKey、完整提示词或正文。
- [ ] 按 `Works → Story/World → AI Session/Tools → Auth/References` 分批发布；每批保留旧 Facade 一个版本周期并可回退。
- [ ] 退出条件：Application 无 Infrastructure 引用，Route 无 EF/Infrastructure using，所有非公开端点有鉴权断言，Application 关键分支覆盖率 ≥80%，`dotnet test` 全绿，漏洞依赖有明确结论。

## 关键取舍

| 决策 | 选择 | 原因 |
|---|---|---|
| 部署形态 | 模块化单体 | 边界尚在收敛；作品、会话和 AI 事务仍需低延迟一致性 |
| 数据访问 | 模块 Store + 聚合端口 | 复杂投影和批量 SQL 不适合无差别 Generic Repository；端口保留可测试性 |
| 读写组织 | CQRS-lite | 先分离命令/查询代码，不为单库 CRUD 引入独立读库 |
| AI 集成 | AI Lib + Application 合同 + Infrastructure 适配器 | 避免 LLM/EF 细节渗透业务用例，同时保留流式能力 |
| 领域建模 | 渐进式丰富实体 | 先建立边界和不变量，再对高价值状态机引入值对象/领域服务 |

## 风险与回退

- 项目引用改动可能产生大量编译错误：每个模块单独提交，保留 Facade 和兼容命名空间。
- Store 可能掩盖 EF 高级能力：在 Infrastructure 增加模块专用 query handler，禁止向 Application 暴露 `IQueryable`。
- AI 工具行为可能变化：固定输入/输出契约测试，旧工具注册键保留一个版本周期。
- 删除级联可能不一致：先在测试数据库检查删除前后孤儿数据，再调整 migration，不直接改生产数据。

## 验证命令

```powershell
dotnet restore
dotnet build SpeakEase.Write.slnx
dotnet test --collect:"XPlat Code Coverage"
dotnet list package --vulnerable
```

每个迁移批次至少执行：

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~Works
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CreationSession
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~DependencyRules
```

