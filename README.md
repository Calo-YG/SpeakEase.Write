# SpeakEase.Write

AI 驱动的长篇小说写作平台，为作者提供从构思到完稿的全流程智能辅助。

## 功能概览

- **作品管理**：支持多作品、分卷、分章的结构化创作，章节级版本管理与自动保存
- **角色体系**：角色档案、关系图谱、角色成长弧线（Character Arc），支持节点-边图模型
- **世界观构建**：势力、地理、历史事件、力量体系、世界规则等设定模块
- **剧情辅助**：伏笔追踪、时间线编排、大纲树形管理
- **AI 智能写作**：基于 ReActAgent 的多智能体协作，支持对话式创作会话、批量生成任务、上下文组装与记忆快照
- **灵感与参考**：灵感记录、参考作品/段落管理、收藏夹
- **导出**：支持作品导出为多种格式
- **仪表盘**：创作数据概览与统计
- **用户系统**：JWT 认证、用户偏好、AI 模型配置

## 技术栈

| 层 | 技术 |
|---|---|
| 运行时 | .NET 10 |
| Web 框架 | ASP.NET Core (Minimal API) |
| 数据库 | PostgreSQL (via Npgsql + EF Core) |
| 缓存 | Redis |
| 认证 | JWT Bearer |
| 日志 | Serilog |
| API 文档 | OpenAPI + Scalar |
| AI 引擎 | 自研 SpeakEase.AI.Lib（ReActAgent + LLM 协议 + 工具框架） |
| 搜索 | Bing Search API |
| ID 生成 | Snowflake 算法 |

## 项目结构

```
SpeakEase.Write.slnx
├── AINWZ/                          # Web 启动层 (Minimal API)
│   ├── MapRoute/                   # 路由端点定义 (按模块)
│   ├── Middleware/                  # 中间件（全局异常、限流、请求计时）
│   ├── HealthChecks/               # 健康检查
│   └── wwwroot/skills/             # AI Agent 技能定义
├── AINWZ.Application/              # 应用层
│   ├── Contracts/{Module}/         # 接口 + DTO
│   └── Applications/               # 接口实现
├── AINWZ.Domain/                   # 领域层
│   ├── Entities/{Module}/          # 领域实体
│   └── Repositories/               # 仓储接口
├── AINWZ.Infrastructure/           # 基础设施层
│   ├── AI/                         # AI 编排（Orchestrator/Tools/Agents）
│   ├── Persistence/                # EF Core DbContext + 迁移
│   ├── Repositories/               # 仓储实现
│   ├── Shared/                     # 通用工具（ApiResult, JsonHelper）
│   └── {Module}/                   # 基础设施服务
├── SpeakEase.AI.Lib/               # AI 核心库
│   ├── AI/                         # ReActAgent 实现
│   ├── LLM/                        # LLM 协议抽象
│   └── Tools/                      # 工具框架
└── AINWZ.Tests/                    # 测试项目
```

## 快速开始

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 16+
- Redis 7+

### 配置

1. 创建数据库 `speakeasewrite`，执行 EF Core 迁移：

```powershell
dotnet ef database update --project AINWZ.Infrastructure --startup-project AINWZ
```

2. 修改 `AINWZ/appsettings.Development.json` 中的连接字符串和密钥：

```json
{
  "ConnectionStrings": {
    "SpeakEaseWrite": "Host=localhost;Port=5432;Database=speakeasewrite;Username=postgres;Password=your_password"
  },
  "LLM": {
    "ApiKey": "your-openai-api-key"
  }
}
```

3. 启动 Redis（默认连接 `localhost:6379`）。

### 运行

```powershell
dotnet restore
dotnet build SpeakEase.Write.slnx
dotnet run --project AINWZ
```

开发环境下访问 `http://localhost:{port}/scalar/v1` 查看 API 文档。

### 测试

```powershell
dotnet test
```

## API 端点概览

| 模块 | 路径前缀 | 说明 |
|---|---|---|
| Auth | `/api/auth` | 注册、登录、刷新令牌 |
| Users | `/api/users` | 用户信息与偏好 |
| Works | `/api/works` | 作品 CRUD |
| Volumes | `/api/works/{id}/volumes` | 分卷管理 |
| Stories | `/api/works/{id}/stories` | 章节管理（含版本） |
| Characters | `/api/works/{id}/characters` | 角色管理 |
| CharacterGraph | `/api/works/{id}/charactergraph` | 角色关系图谱 |
| CharacterArc | `/api/works/{id}/characterarc` | 角色弧线 |
| Relationships | `/api/works/{id}/relationships` | 角色关系 |
| Foreshadowing | `/api/works/{id}/foreshadowing` | 伏笔管理 |
| Timeline | `/api/works/{id}/timeline` | 时间线 |
| World | `/api/works/{id}/world` | 世界观设定 |
| Inspiration | `/api/works/{id}/inspiration` | 灵感记录 |
| References | `/api/works/{id}/references` | 参考作品/段落 |
| Tags | `/api/tags` | 标签管理 |
| AI / Sessions | `/api/ai` | AI 写作会话 (SSE 流式) |
| Dashboard | `/api/dashboard` | 创作仪表盘 |
| Export | `/api/works/{id}/export` | 作品导出 |
| Models | `/api/models` | AI 模型管理 |
| Health | `/health/live`, `/health/ready` | 健康检查 |

## 架构要点

- **接口 + 实现分离**：Application 层每个服务拆分为 `Contracts/{Module}/I{Name}Application.cs` 接口与 `Applications/{Name}Application.cs` 实现
- **DTO 独立文件**：请求/响应 DTO 位于 `Contracts/{Module}/Dto/`，每个类一个文件
- **EF Core 直接查询**：Application 层直接使用 `SpeakEaseDbContext` 进行查询，复杂聚合走仓储接口
- **只读查询**：所有查询使用 `AsNoTracking()`
- **批量操作**：使用 `ExecuteDeleteAsync` / `ExecuteUpdateAsync` 避免加载到内存
- **分页**：先查 ID 列表再按 ID 查详情，避免全表扫描
- **SSE 流式**：AI 写作会话返回 `text/event-stream`，支持实时逐字输出
- **多级缓存**：Memory Cache + Redis 两级缓存架构

## 提交规范

```
<type>: <简短描述>
```

示例：`feat: add chapter version manager` / `fix: correct session expiry logic`
