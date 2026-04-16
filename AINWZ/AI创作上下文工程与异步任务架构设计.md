# AI创作上下文工程与异步任务架构设计

> 日期：2026-04-16
> 状态：待确认

---

## 一、整体架构概览

```
用户操作（保存章节/续写/润色）
        │
        ▼
┌─────────────────────────────────┐
│         Application 层          │
│  WorkApplication / ChapterApp   │
│  - 拥有 Prompt 所有权           │
│  - 调用 IContextAssembler 取数据 │
│  - 自行拼接最终 SystemPrompt    │
│  - 调用 ILLMService 执行        │
└──────────┬──────────────────────┘
           │
     ┌─────┴──────┐
     ▼            ▼
┌─────────┐ ┌──────────┐
│Context  │ │LLMService│
│Assembler│ │  执行器   │
│数据组装  │ │ ChatAsync│
│不构造Prompt│ │StreamAsync│
└────┬────┘ └──────────┘
     │
     ▼
┌─────────────────────────────┐
│       Infrastructure 层      │
│  - DbContext（查询实体数据）  │
│  - IMemoryCache（缓存设定）   │
│  - MemorySnapshot（同步写入） │
│  - ContextAssemblyLog（写日志）│
└─────────────────────────────┘
```

### 职责边界

| 层 | 职责 | 不负责 |
|---|---|---|
| **Application** | Prompt拼接、业务流程编排、参数验证 | 数据格式化 |
| **IContextAssembler (Infrastructure)** | 查DB + 缓存 + 格式化为文本段落 + Token估算 + 裁剪 | 不构造SystemPrompt、不决定Prompt措辞 |
| **ILLMService (Infrastructure)** | 执行LLM调用、流式返回 | 不知道业务语义 |

---

## 二、两层上下文架构

原三层架构已简化为两层（MemoryChunk已删除，AuthorNotes归入第二层）：

```
第一层：作品级设定（缓存，变更时主动失效）
├── 世界观摘要（WorldSetting.Summary + EraBackground + OverallStyle）
├── 世界规则（WorldRuleEntity 列表）
├── 力量体系（PowerSystemEntity 列表）
├── 势力（FactionEntity 列表）
├── 地理（GeographyEntity 列表）
├── 历史事件（HistoricalEventEntity 列表）
├── 角色档案（Characters: Name+Identity+Personality+Motivation+AbilityDescription）
├── 角色关系（CharacterRelationships）
├── 大纲节点（OutlineNodes: Title+Goal+KeyEvent+Sequence）
└── 伏笔追踪（Foreshadowings where Status=pending）

第二层：章节级上下文（实时查询，不缓存）
├── 当前章节全文（Chapter.Content）
├── 当前章节作者备注（Chapter.AuthorNotes）
└── 前N章摘要（RecentChapterCount 由用户配置，默认2）
```

---

## 三、IContextAssembler 接口设计

### 3.1 接口与模型

位置：`AINWZ.Infrastructure/LLM/Context/`

```csharp
/// <summary>
/// 上下文组装器，负责从数据库收集作品/章节数据并格式化为文本段落。
/// 不负责 Prompt 拼接——Application 层拥有 Prompt 所有权。
/// </summary>
public interface IContextAssembler
{
    Task<AssembledContext> AssembleAsync(
        ContextAssembleRequest request,
        CancellationToken ct = default);
}
```

### 3.2 请求模型

```csharp
/// <summary>
/// 上下文组装请求。
/// </summary>
public class ContextAssembleRequest
{
    public string WorkId { get; set; }
    public string ChapterId { get; set; }
    public string UserId { get; set; }
    public string Mode { get; set; } = "continue";  // string 非枚举，Application 层自定义
    public ContextAssembleOptions Options { get; set; } = new();
}

/// <summary>
/// 用户可配置的组装选项，存储在 WorkEntity.ContextOptionsJson。
/// </summary>
public class ContextAssembleOptions
{
    public bool IncludeWorldSetting { get; set; } = true;
    public bool IncludeCharacters { get; set; } = true;
    public bool IncludeOutline { get; set; } = true;
    public bool IncludeForeshadowing { get; set; } = true;
    public bool IncludeAuthorNotes { get; set; } = true;
    public int RecentChapterCount { get; set; } = 2;
    public int? MaxContextTokens { get; set; }  // null = 不限制
}
```

### 3.3 返回模型

```csharp
/// <summary>
/// 组装结果，返回格式化后的文本段落，由 Application 层自行拼入 SystemPrompt。
/// </summary>
public class AssembledContext
{
    /// <summary>
    /// 第一层：作品级设定，已格式化为文本段落。
    /// Key = 段落名称，Value = 段落内容。
    /// </summary>
    public Dictionary<string, string> WorldSections { get; set; } = new();

    /// <summary>
    /// 第二层：章节级上下文，已格式化为文本段落。
    /// </summary>
    public Dictionary<string, string> ChapterSections { get; set; } = new();

    /// <summary>
    /// 组装指标。
    /// </summary>
    public ContextAssembleMetrics Metrics { get; set; } = new();
}

/// <summary>
/// 组装指标，用于写入 ContextAssemblyLogEntity。
/// </summary>
public class ContextAssembleMetrics
{
    public string ContextMode { get; set; }
    public string SnapshotId { get; set; }
    public int WorldSettingTokens { get; set; }      // 第一层 token
    public int ChapterContextTokens { get; set; }     // 第二层 token
    public int TotalTokens { get; set; }
    public List<string> IncludedSections { get; set; } = new();
    public List<string> TrimmedSections { get; set; } = new();  // 被裁剪掉的段落
}
```

### 3.4 Application 层使用示例

```csharp
// WorkApplication 中
var context = await _contextAssembler.AssembleAsync(new ContextAssembleRequest
{
    WorkId = workId,
    ChapterId = chapterId,
    UserId = userId,
    Mode = "continue",
    Options = options
}, ct);

// Application 层拥有 Prompt 所有权——自行拼接最终 SystemPrompt
var sb = new StringBuilder();
sb.AppendLine("你是一位专业的小说创作助手，正在协助用户续写小说。");
sb.AppendLine();
foreach (var section in context.WorldSections)
    sb.AppendLine($"【{section.Key}】\n{section.Value}");
foreach (var section in context.ChapterSections)
    sb.AppendLine($"【{section.Key}】\n{section.Value}");

var request = new LLMChatRequest
{
    SystemPrompt = sb.ToString(),
    Messages = [new LLMChatMessage { Role = "user", Content = "请续写下一段内容" }],
    UseJsonMode = false,
    Temperature = 0.9m
};
```

---

## 四、ContextAssembler 实现细节

### 4.1 组装流程

```
AssembleAsync(request)
│
├── 1. 读取 WorkEntity.ContextOptionsJson，合并用户配置
│   （如果为空，使用默认值）
│
├── 2. 收集第一层数据（优先从 IMemoryCache 取）
│   ├── WorldSetting + 子表（WorldRules/PowerSystems/Factions/Geographies/HistoricalEvents）
│   ├── Characters + CharacterRelationships
│   ├── OutlineNodes（按 Sequence 排序）
│   └── Foreshadowings（Status=pending）
│   → 格式化为 Dictionary<string, string> WorldSections
│
├── 3. 收集第二层数据（实时查询，不缓存）
│   ├── 当前 Chapter（Content + AuthorNotes）
│   └── 前 N 章 Summary（按 Sequence DESC 取 N 条）
│   → 格式化为 Dictionary<string, string> ChapterSections
│
├── 4. Token 估算
│   ├── 中文1字 ≈ 1.5 token，其他 ≈ 1 token
│   └── 若超 MaxContextTokens，按优先级裁剪第二层
│
├── 5. 同步写入 MemorySnapshotEntity
│   └── SnapshotJson = 本轮注入的完整数据
│
└── 6. 返回 AssembledContext
```

### 4.2 缓存策略

```csharp
// 缓存 Key 模式
cache-key: "ctx:{workId}:world-setting"
cache-key: "ctx:{workId}:characters"
cache-key: "ctx:{workId}:outline"
cache-key: "ctx:{workId}:foreshadowing"

// 过期时间：30 分钟
// 失效时机：WorkApplication 更新对应实体时主动 Remove
```

### 4.3 裁剪优先级

当总 Token 超过 MaxContextTokens 时，按以下优先级从低到高裁剪：

| 裁剪顺序 | 段落 | 原因 |
|----------|------|------|
| 1（先删） | 历史事件 | 对续写影响最小 |
| 2 | 地理 | 低频参考 |
| 3 | 势力 | 低频参考 |
| 4 | 前N章摘要（从最旧开始） | 越旧越不重要 |
| 5 | 力量体系 | 中频参考 |
| 6 | 世界规则 | 中频参考 |
| 7 | 伏笔列表 | 高频参考 |
| 8 | 大纲节点 | 高频参考 |
| 9 | 角色档案 | 极高频 |
| 10 | 世界观摘要 | 核心设定 |
| 11（不删） | 当前章节全文 + AuthorNotes | 正在写的内容 |

### 4.4 Token 估算方法

```csharp
private static int EstimateTokens(string text)
{
    if (string.IsNullOrEmpty(text)) return 0;
    var chineseCount = text.Count(c => c > 0x4E00 && c < 0x9FFF);
    var otherCount = text.Length - chineseCount;
    return (int)(chineseCount * 1.5) + otherCount;
}
```

不引入 TikToken 等第三方库，粗估即可满足裁剪需求。

---

## 五、WorkEntity 扩展

新增字段存储作品级上下文配置：

```csharp
// WorkEntity 新增
/// <summary>
/// 上下文组装配置 JSON，存储 ContextAssembleOptions。
/// 为空时使用默认配置。
/// </summary>
public string ContextOptionsJson { get; set; } = string.Empty;
```

EF Configuration：
```csharp
builder.Property(x => x.ContextOptionsJson).HasColumnType("jsonb");
```

---

## 六、异步任务体系

### 6.1 触发场景

```
用户保存章节内容
    │
    ├──► 同步：更新 ChapterEntity + WordCount + LastContentSavedAt
    │
    └──► 异步入队（IBackgroundTaskQueue）
         ├── TaskType = "analyze-foreshadowing"
         ├── TaskType = "analyze-character-arc"
         └── TaskType = "generate-summary"
```

### 6.2 后台任务队列

```csharp
// AINWZ.Infrastructure/Tasks/IBackgroundTaskQueue.cs
public interface IBackgroundTaskQueue
{
    void Enqueue(string taskType, string taskId);
    Task<(string TaskType, string TaskId)> DequeueAsync(CancellationToken ct);
}

// 实现：基于 Channel<T> 的内存队列
// AINWZ.Infrastructure/Tasks/BackgroundTaskQueue.cs
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<(string, string)> _channel =
        Channel.CreateBounded<(string, string)>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public void Enqueue(string taskType, string taskId)
        => _channel.Writer.TryWrite((taskType, taskId));

    public async Task<(string, string)> DequeueAsync(CancellationToken ct)
        => await _channel.Reader.ReadAsync(ct);
}
```

### 6.3 后台任务执行器

```csharp
// AINWZ.Application/Tasks/IAITaskExecutor.cs
public interface IAITaskExecutor
{
    Task ExecuteAsync(string taskType, string taskId, CancellationToken ct);
}
```

### 6.4 HostedService 消费者

```csharp
// AINWZ/AITaskHostedService.cs
public class AITaskHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceProvider _serviceProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (taskType, taskId) = await _queue.DequeueAsync(stoppingToken);
            // 每个任务在独立 Scope 中执行
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAITaskExecutor>();
            await executor.ExecuteAsync(taskType, taskId, stoppingToken);
        }
    }
}
```

### 6.5 启动兜底

程序启动时扫描 Status=pending 的 AIGenerationTaskEntity，重新入队：

```csharp
// AINWZ/AITaskHostedService.cs 中增加
public override async Task StartAsync(CancellationToken ct)
{
    // 兜底：重新入队未完成的任务
    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AINWZDbContext>();
    var pendingTasks = await db.AIGenerationTasks
        .Where(t => t.Status == "pending" || t.Status == "processing")
        .ToListAsync(ct);
    foreach (var task in pendingTasks)
    {
        task.Status = "pending";  // 重置 processing → pending
        _queue.Enqueue(task.TaskType, task.Id);
    }
    await db.SaveChangesAsync(ct);

    await base.StartAsync(ct);
}
```

### 6.6 任务执行流程

```
AITaskExecutor.ExecuteAsync(taskType, taskId)
│
├── 1. 查 AIGenerationTaskEntity，更新 Status = "processing"
│
├── 2. 调用 IContextAssembler.AssembleAsync(mode="analyze")
│
├── 3. 构造领域专属 Prompt（Application 层拥有 Prompt 所有权）
│   ├── analyze-foreshadowing: 注入已知伏笔列表 + 当前章节，UseJsonMode=true
│   ├── analyze-character-arc: 注入角色列表 + 当前章节，UseJsonMode=true
│   └── generate-summary: 注入当前章节，UseJsonMode=true
│
├── 4. 调用 ILLMService.ChatAsync
│
├── 5. 解析 JSON 结果
│   ├── analyze-foreshadowing:
│   │   ├── 创建 ChapterAnalysisResultEntity（AnalysisType="foreshadowing"）
│   │   └── 自动创建 ForeshadowingEntity（Status="ai-detected"，待用户确认）
│   ├── analyze-character-arc:
│   │   ├── 创建 ChapterAnalysisResultEntity（AnalysisType="character-arc"）
│   │   └── 自动创建 CharacterArcEntity（标记来源 ai-generated）
│   └── generate-summary:
│       └── 更新 ChapterEntity.Summary
│
├── 6. 更新 AIGenerationTaskEntity Status = "completed"
│
└── 7. 通过 SignalR 推送通知前端
    └── "AI 检测到 2 条新伏笔线索"
```

---

## 七、SignalR 集成

### 7.1 Hub 定义

```csharp
// AINWZ/Hubs/AINWHub.cs
public class AINWHub : Hub
{
    // 前端调用：加入作品房间
    public Task JoinWorkRoom(string workId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"work:{workId}");

    // 前端调用：离开作品房间
    public Task LeaveWorkRoom(string workId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"work:{workId}");
}
```

### 7.2 推送事件定义

```csharp
// AINWZ.Infrastructure/SignalR/AINWHubEvents.cs
public class AINWHubEvents
{
    public const string TaskCompleted = "task-completed";       // 异步任务完成
    public const string AnalysisResult = "analysis-result";     // 分析结果就绪
    public const string StreamChunk = "stream-chunk";           // 流式续写片段
    public const string StreamEnd = "stream-end";               // 流式续写结束
}
```

### 7.3 推送方式

```csharp
// 在 AITaskExecutor 执行完成后
var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AINWHub>>();
await hubContext.Clients.Group($"work:{workId}")
    .SendAsync(AINWHubEvents.AnalysisResult, new
    {
        TaskType = taskType,
        TaskId = taskId,
        ChapterId = chapterId,
        Summary = "检测到2条新伏笔线索"
    }, ct);
```

### 7.4 Program.cs 注册

```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<AINWHub>("/hubs/ainw");
```

### 7.5 鉴权

SignalR Hub 复用已有的 JWT 鉴权中间件：

```csharp
builder.Services.AddSignalR()
    .AddBearerAuthentication();  // JWT 自动传递

// AINWHub 中可获取用户
var userId = Context.User?.FindFirst("sub")?.Value;
```

---

## 八、MemorySnapshot 设计

### 8.1 快照内容

每章完成时或每次 AI 调用前，同步写入 MemorySnapshotEntity：

```json
{
  "worldSetting": {
    "summary": "...",
    "eraBackground": "...",
    "overallStyle": "..."
  },
  "characters": [
    { "id": "...", "name": "...", "identity": "...", "personality": "..." }
  ],
  "outlineNodes": [
    { "id": "...", "title": "...", "goal": "...", "keyEvent": "..." }
  ],
  "foreshadowings": [
    { "id": "...", "title": "...", "status": "pending" }
  ],
  "recentChapterSummaries": ["...", "..."],
  "currentChapter": {
    "id": "...",
    "title": "...",
    "authorNotes": "...",
    "wordCount": 1234
  },
  "contextOptions": {
    "includeWorldSetting": true,
    "recentChapterCount": 2
  },
  "timestamp": "2026-04-16T..."
}
```

### 8.2 快照写入策略

- **同步写入**，不异步（审计数据不可丢）
- 写入失败 → AssembleAsync 整体失败
- 每章完成时做一次"完成快照"（SnapshotType = "chapter-complete"）
- AI 调用前做一次"调用快照"（SnapshotType = "pre-call"）

---

## 九、伏笔/角色分析 Prompt 设计

### 9.1 伏笔分析

```
SystemPrompt（Application 层构造）:

你是专业的小说分析助手。根据以下章节内容和已知的角色、世界观设定，
分析本章内容，提取：

1. 新埋设的伏笔线索（标题+描述+预估重要度1-5）
2. 已回收的伏笔（如果本章解决了之前的某个伏笔）
3. 角色状态变化（哪个角色+变化前+变化后+触发事件）

【已知伏笔】
{Foreshadowings where Status=pending}

【角色档案】
{Characters 简要列表}

【本章内容】
{Chapter.Content}

严格以JSON格式输出：
{
  "newForeshadowings": [{"title":"","description":"","importance":3}],
  "resolvedForeshadowings": [{"id":"","title":"","originalDescription":""}],
  "characterArcs": [{"characterName":"","previousState":"","newState":"","triggerEvent":""}]
}

如果没有发现，返回空数组。不要编造不存在的伏笔。
```

- UseJsonMode = true
- Temperature = 0.5

### 9.2 章节摘要生成

```
SystemPrompt:

请为以下章节内容生成一份简洁的摘要（200字以内），包含：
1. 关键事件
2. 角色变化
3. 伏笔线索

【章节标题】{Chapter.Title}
【章节内容】
{Chapter.Content}

严格以JSON格式输出：
{
  "summary": "...",
  "keyEvents": ["...", "..."],
  "characterChanges": ["..."],
  "foreshadowingHints": ["..."]
}
```

- UseJsonMode = true
- Temperature = 0.5

---

## 十、完整流程图

### 10.1 续写流程

```
用户点击"AI续写"
    │
    ▼
WorkApplication.ContinueAsync(workId, chapterId, userId)
    │
    ├── 1. 参数验证
    │
    ├── 2. 读取 WorkEntity.ContextOptionsJson → ContextAssembleOptions
    │
    ├── 3. 调用 IContextAssembler.AssembleAsync(request)
    │   ├── 查 DB + 缓存 → 收集第一层数据
    │   ├── 查 DB → 收集第二层数据
    │   ├── Token 估算 + 裁剪
    │   ├── 同步写 MemorySnapshot
    │   └── 返回 AssembledContext
    │
    ├── 4. Application 层拼接 SystemPrompt
    │   └── "你是小说创作助手..." + WorldSections + ChapterSections
    │
    ├── 5. 写 ContextAssemblyLogEntity
    │
    ├── 6. 调用 ILLMService.StreamAsync（流式返回）
    │   ├── 每个流式片段 → SignalR 推送给前端
    │   └── 流结束 → SignalR 推送 stream-end
    │
    └── 7. 返回 ApiResult<StreamSession>
```

### 10.2 保存章节 + 异步分析流程

```
用户保存章节内容
    │
    ▼
WorkApplication.SaveChapterContentAsync(workId, chapterId, content)
    │
    ├── 1. 更新 ChapterEntity（Content, WordCount, LastContentSavedAt）
    │
    ├── 2. 使缓存失效（如果涉及角色/世界观变更）
    │
    ├── 3. 创建 AIGenerationTaskEntity（Status=pending）
    │   ├── TaskType = "analyze-foreshadowing"
    │   ├── TaskType = "analyze-character-arc"
    │   └── TaskType = "generate-summary"
    │
    ├── 4. IBackgroundTaskQueue.Enqueue(taskType, taskId) × 3
    │
    └── 5. 立即返回 ApiResult（不等待异步任务）

─────────────────────────────────────────

后台 AITaskHostedService 消费队列
    │
    ▼
AITaskExecutor.ExecuteAsync(taskType, taskId)
    │
    ├── 查 AIGenerationTaskEntity → Status = "processing"
    │
    ├── IContextAssembler.AssembleAsync(mode="analyze")
    │
    ├── 构造 Prompt + ILLMService.ChatAsync
    │
    ├── 解析结果 → 创建 ChapterAnalysisResultEntity
    │   └── 自动创建 ForeshadowingEntity / CharacterArcEntity
    │
    ├── AIGenerationTaskEntity → Status = "completed"
    │
    └── SignalR 推送 AnalysisResult → 前端显示通知

─────────────────────────────────────────

前端收到 SignalR 通知
    │
    ▼
用户查看分析结果 → 逐条确认/忽略/修改
    │
    ▼
WorkApplication.ConfirmAnalysisResultAsync(resultId, feedback)
    │
    ├── IsConfirmed = true/false
    ├── UserFeedback = "accepted"/"ignored"/"modified"
    └── 如果 ignored → 删除自动创建的 ForeshadowingEntity
```

### 10.3 章节完成流程

```
用户标记章节为"已完成"
    │
    ▼
WorkApplication.CompleteChapterAsync(workId, chapterId)
    │
    ├── 1. ChapterEntity.Status = "completed"
    │
    ├── 2. 触发 MemorySnapshot（SnapshotType = "chapter-complete"）
    │   └── 同步写入 MemorySnapshotEntity
    │
    ├── 3. 更新 WorkEntity.TotalWordCount
    │
    └── 4. 返回 ApiResult
```

---

## 十一、实体改动汇总

| 实体 | 改动 | 说明 |
|------|------|------|
| WorkEntity | + ContextOptionsJson (jsonb) | 存储作品级上下文配置 |
| ChapterEntity | + AuthorNotes (text) | ✅ 已完成 |
| ContextAssemblyLogEntity | RetrievedContextTokens 含义变更 | 第三层已移除，此字段现记录 AuthorNotes tokens |
| MemorySnapshotEntity | 无改动 | SnapshotJson 内容定义已明确 |

---

## 十二、待实现文件清单

### Infrastructure 层

| 文件 | 说明 |
|------|------|
| `LLM/Context/IContextAssembler.cs` | 接口定义 |
| `LLM/Context/ContextAssembler.cs` | 实现 |
| `LLM/Context/ContextAssembleRequest.cs` | 请求模型 |
| `LLM/Context/ContextAssembleOptions.cs` | 用户配置模型 |
| `LLM/Context/AssembledContext.cs` | 返回模型 |
| `LLM/Context/ContextAssembleMetrics.cs` | 指标模型 |
| `Tasks/IBackgroundTaskQueue.cs` | 后台任务队列接口 |
| `Tasks/BackgroundTaskQueue.cs` | Channel 实现 |
| `LLM/ServiceCollectionExtensions.cs` | 注册 IContextAssembler |

### Application 层

| 文件 | 说明 |
|------|------|
| `Tasks/IAITaskExecutor.cs` | 任务执行器接口 |
| `Tasks/AITaskExecutor.cs` | 实现 |
| `Contracts/Works/Dto/ContextOptionsResponse.cs` | 上下文配置 DTO |

### AINWZ 主项目

| 文件 | 说明 |
|------|------|
| `AITaskHostedService.cs` | 后台 HostedService |
| `Hubs/AINWHub.cs` | SignalR Hub |
| `Program.cs` | 注册 SignalR + HostedService |

---

## 十三、已确认决策索引

1. **IContextAssembler 放 Infrastructure 层** — 接口+实现都在 Infrastructure
2. **Application 层拥有 Prompt 所有权** — ContextAssembler 只返回文本段落，不构造 SystemPrompt
3. **ContextMode 用 string 而非 enum** — Application 层自定义模式名，支持扩展
4. **MemoryChunk 已删除，AuthorNotes 替代** — 两层架构
5. **第一层缓存，第二层实时查** — IMemoryCache + 主动失效
6. **Token 裁剪按优先级** — 非纯时间序
7. **MemorySnapshot 同步写入** — 审计数据不可丢
8. **任务结果直接用 SignalR** — 不做轮询过渡
9. **异步任务用内存队列** — Channel<T>，启动时兜底 re-queue
10. **伏笔分析 UseJsonMode=true** — 严格 JSON 输出，不编造
