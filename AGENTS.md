# 开发规范

## 项目分层结构

```
AINWZ/                    — ASP.NET Core Web 启动层
AINWZ.Application/        — 应用层：业务编排 + 接口 + DTO
  ├── Contracts/{Module}/  — 接口定义 (I{Name}Application.cs)
  │     └── Dto/           — 请求/响应 DTO（每个文件一个类）
  └── Applications/        — 接口实现 ({Name}Application.cs)
AINWZ.Domain/             — 领域层：实体 + 仓储接口
  ├── Entities/{Module}/   — 领域实体
  └── Repositories/        — 仓储接口
AINWZ.Infrastructure/     — 基础设施层：持久化 + 集成 + 工具
  ├── AI/                  — AI 编排相关（Orchestrator/Tools/Agents）
  ├── Persistence/         — EF Core DbContext + 配置 + 迁移
  ├── Repositories/        — 仓储实现
  ├── Shared/              — 通用工具类（ApiResult, JsonHelper 等）
  └── {Module}/            — 按功能模块划分
SpeakEase.AI.Lib/         — AI 核心库：ReActAgent + LLM 协议 + 工具框架
AINWZ.Tests/              — 测试项目
```

## 代码规范

### 通用规范
- **Nullable 上下文**：整个解决方案禁用 `#nullable enable`（`<Nullable>disable</Nullable>`）
- **缩进**：4 空格
- **命名**：PascalCase（类型/方法/公开成员）、camelCase（局部变量/参数）
- **using 顺序**：先系统 using → 空行 → NuGet 包 using → 空行 → 项目内部 using

### Application 层规范

**1. 接口 + 实现模式** — 每个 Application 服务必须拆分为接口和实现：

```
Contracts/{Module}/I{Name}Application.cs  →  接口定义
Applications/{Name}Application.cs         →  实现
```

接口和 DTO 命名空间保持一致：`SpeakEase.Write.Application.Contracts.{Module}`

**2. DTO 规范**：
- 每个 DTO 类独立一个文件 `Contracts/{Module}/Dto/{Name}.cs`
- 不在业务类文件底部定义 DTO
- 请求 DTO 统一后缀 `Request`，响应 DTO 统一后缀 `Response`
- DTO 使用 `string.Empty` 而非 `null` 作为默认值

**3. 实现类规范**：
- 优先使用 `SpeakEaseDbContext` 直接操作数据库（当前项目不使用仓储模式进行复杂查询）
- 使用 `IUserContext` 获取当前用户信息
- 使用 `ISnowflakeIdGenerator` 生成主键 ID
- 返回 `ApiResult<T>`（成功）或 `ApiResult`（仅状态）
- EF Core 查询使用 `AsNoTracking()` 用于只读查询

**4. 跨层引用规则**：
- Application 层可以引用 Infrastructure 层的 `Shared/`（如 `JsonHelper`、`ApiResult`）
- Application 层**不应**直接引用 Infrastructure 层的业务实现（如 `BlackboardHolder`）
- 需要引用时，通过接口抽象层隔开，或将逻辑提升到 Application 层

### Infrastructure 层规范

- 基础设施服务（如 `MultiCacheService`、`SnowflakeIdGenerator`、`OpenAIContext`）可以不定义接口
- DI 扩展方法集中在 `{Module}ServiceCollectionExtensions.cs` 文件中
- EF Core 配置类放在 `Persistence/Configurations/{Module}/` 下

### API 路由层规范

路由文件命名和映射：

```
MapRoute/{Module}/{Name}Route.cs
  → 命名空间：SpeakEase.Write.MapRoute.{Module}
  → 扩展方法：public static void Map{Name}EndPoint(this IEndpointRouteBuilder app)
  → 在 Program.cs 中调用：app.Map{Name}EndPoint()
```

### EF Core 查询规范

- **IO-bound 操作批量删除/更新**：使用 `ExecuteDeleteAsync` / `ExecuteUpdateAsync`（不加载到内存）
- **分页查询**：先查 ID 列表，再按 ID 查详情（避免 SELECT * 到内存再分页）
- **事务**：涉及多表写操作时使用 `await using var transaction = await dbContext.Database.BeginTransactionAsync()`
- **只读查询**：始终追加 `.AsNoTracking()`
- **避免全表加载**：不在 Application 层 `await foreach` 遍历 DbSet，使用 `Take()` 限制结果集

### 路由端点规范

- **增删改查**端点使用 RESTful 路径
- **业务动作**端点使用 `POST {resource}/{id}/{action}` 模式
- **SSE 流式**端点返回 `Content-Type: text/event-stream`
- 所有端点（除了注册/登录/刷新令牌）都需要 `.RequireAuthorization()`

## 构建命令

```powershell
dotnet restore
dotnet build AINWZ.slnx
dotnet run --project AINWZ/AINWZ.csproj
dotnet test
```

## 提交规范

`<type>: <简短描述>`  
示例：`feat: add chapter version manager` / `fix: correct session expiry logic`

## 测试规范

- 测试文件命名：`{UnitUnderTest}Tests.cs`
- Arrange → Act → Assert 结构
- 优先对业务逻辑（Application 层）编写单元测试
