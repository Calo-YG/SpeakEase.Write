# Multi-Agent 小说创作系统架构设计

## 一、总体架构

```
┌─────────────────────────────────────────────────────────┐
│                      前端 (SSE 流)                       │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│                 CreationOrchestrator                     │
│                 （创作编排器 - 应用层）                    │
│                                                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │ Router   │→│ Agent    │→│ Black-   │→│ Stream  │ │
│  │ (路由决策) │  │ Executor │  │ board    │  │ Merger  │ │
│  └──────────┘  └──────────┘  └──────────┘  └─────────┘ │
└──────────────────────┬──────────────────────────────────┘
                       │
     ┌─────────────────┼─────────────────┐
     ▼                 ▼                 ▼
┌──────────┐    ┌──────────┐    ┌──────────┐
│ World-   │    │ Outline- │    │ Write-   │
│ Agent    │    │ Agent    │    │ Agent    │
└──────────┘    └──────────┘    └──────────┘
┌──────────┐    ┌──────────┐
│ Creation-│    │ Audit-   │
│ Agent    │    │ Agent    │
└──────────┘    └──────────┘
     │                │
     └────────────────┘
        共用底层能力
┌─────────────────────────────────────────────────────────┐
│              ReActAgent (通用 LLM 对话引擎)              │
│              OpenAIContext (LLM 配置解析)                │
│              HybridMemoryProvider (混合记忆)            │
│              SSEForwardProvider (流式转发)              │
└─────────────────────────────────────────────────────────┘
```

---

## 二、路由系统（Router）

路由分为三层：关键词匹配 → LLM 分类 → 多 Agent 流水线

### 2.1 路由结果模型

```csharp
public sealed class RouteResult
{
    public string AgentName { get; set; }    // write | world | outline | creation | audit
    public string ContentType { get; set; }  // chapter | character | outline | setting | audit_report | plain
    public string Reason { get; set; }
    public string WorkId { get; set; }
    public List<string> Pipeline { get; set; } // 多 Agent 流水线时使用
}
```

### 2.2 路由判断实现

```csharp
public sealed class CreationRouter
{
    // 第一层：关键词匹配（0 LLM 调用）
    private static readonly Dictionary<string, (string Agent, string ContentType)> KeywordRules = new()
    {
        { "写",      ("write",    "chapter") },
        { "续写",    ("write",    "chapter") },
        { "润色",    ("write",    "chapter") },
        { "大纲",    ("outline",  "outline") },
        { "情节",    ("outline",  "outline") },
        { "规划",    ("outline",  "outline") },
        { "世界观",  ("world",    "setting") },
        { "设定",    ("world",    "setting") },
        { "世界",    ("world",    "setting") },
        { "角色",    ("creation", "character") },
        { "人物",    ("creation", "character") },
        { "创意",    ("creation", "plain") },
        { "点子",    ("creation", "plain") },
        { "脑洞",    ("creation", "plain") },
        { "检查",    ("audit",    "audit_report") },
        { "审阅",    ("audit",    "audit_report") },
        { "审核",    ("audit",    "audit_report") },
    };

    public async Task<RouteResult> DecideAsync(string userMessage, CancellationToken ct)
    {
        // 第一层：关键词匹配
        foreach (var (keyword, (agent, contentType)) in KeywordRules)
        {
            if (userMessage.Contains(keyword))
                return new RouteResult
                {
                    AgentName = agent,
                    ContentType = contentType,
                    Reason = $"关键词「{keyword}」匹配 → {agent}"
                };
        }

        // 第二层：LLM 轻量分类（便宜模型，单次调用）
        var classification = await ClassifyWithLLMAsync(userMessage, ct);
        return classification;
    }

    // 第三层：检测是否是多 Agent 需求（用户一句话涉及多个方面）
    public bool TryDetectPipeline(string userMessage, out List<string> pipeline)
    {
        // 例如 "我想写一个修仙世界观下的悬疑故事，先帮我把大纲列出来"
        // → ["world", "outline"]
        pipeline = null;
        return false;
    }
}
```

### 2.3 LLM 分类 Prompt

```
你是一个小说创作助手路由分类器。分析用户输入，只返回一个 Agent 名称：

- write: 用户要求实际写作、续写、扩写、润色
- outline: 用户要求规划大纲、情节结构、章节安排
- world: 用户要求构建世界观、设定、地理、势力
- creation: 用户要求创意点子、脑洞、角色设计
- audit: 用户要求检查审阅、逻辑一致性

返回格式: {"agent": "write", "reason": "..."}
```

---

## 三、黑板模式（Blackboard）

所有 Agent 共享一个结构化上下文，各 Agent 读写自己负责的部分。

### 3.1 黑板数据结构

```csharp
public sealed class WritingBlackboard
{
    public string WorkId { get; set; }
    public string RequestId { get; set; }

    // 世界观设定（WorldAgent 读写）
    public WorldSettingSection WorldSetting { get; set; }

    // 大纲结构（OutlineAgent 读写）
    public OutlineSection Outline { get; set; }

    // 人物列表（CreationAgent 读写）
    public List<CharacterSection> Characters { get; set; }

    // 最近章节内容（WriteAgent 读写）
    public List<ChapterSection> RecentChapters { get; set; }

    // 一致性检查结果（AuditAgent 读写）
    public List<AuditResultSection> AuditResults { get; set; }

    // 所有 Agent 都能读的元信息
    public WritingMetaInfo Meta { get; set; }
}

public sealed class WorldSettingSection
{
    public string WorldRules { get; set; }      // 世界规则（魔法/科技体系）
    public string Geography { get; set; }       // 地理与文明
    public string Factions { get; set; }        // 势力与政治
    public string History { get; set; }         // 历史编年
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class OutlineSection
{
    public List<VolumeNode> Volumes { get; set; }
    public string OverallArc { get; set; }      // 整体故事弧线
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class VolumeNode
{
    public int Sequence { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public List<ChapterNode> Chapters { get; set; }
}

public sealed class ChapterNode
{
    public int Sequence { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public string Status { get; set; } // outline | written | revised
}

public sealed class CharacterSection
{
    public string CharacterId { get; set; }
    public string Name { get; set; }
    public string CoreSeed { get; set; }       // 核心种子（用户给的）
    public string Background { get; set; }     // 背景故事
    public string Personality { get; set; }    // 性格特征
    public string Traits { get; set; }         // 习惯/小动作
    public string Voice { get; set; }          // 说话风格
    public string Arc { get; set; }            // 成长弧线
    public List<string> Relationships { get; set; } // 人际关系
    public List<string> Fears { get; set; }    // 恐惧
    public List<string> Desires { get; set; }  // 欲望
    public DateTime LastGrowthAt { get; set; }
}

public sealed class ChapterSection
{
    public string ChapterId { get; set; }
    public int Sequence { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string Summary { get; set; }
}

public sealed class AuditResultSection
{
    public string CheckType { get; set; }      // consistency | character | timeline | plot_hole
    public string Severity { get; set; }       // high | medium | low
    public string Description { get; set; }
    public string Suggestion { get; set; }
}

public sealed class WritingMetaInfo
{
    public string Genre { get; set; }          // 小说类型
    public string Perspective { get; set; }    // 叙事视角
    public List<string> StyleTags { get; set; } // 风格标签
    public int TotalWordCount { get; set; }
    public string CurrentFocus { get; set; }   // 当前创作焦点
}
```

### 3.2 黑板构建器

```csharp
public sealed class WritingBlackboardBuilder
{
    private readonly SpeakEaseDbContext _db;
    private readonly IMemoryProvider _memory;

    public async Task<WritingBlackboard> BuildAsync(string workId, string requestId)
    {
        var work = await _db.Works.FirstOrDefaultAsync(x => x.Id == workId);
        var chapters = await _db.Chapters.Where(x => x.WorkId == workId)
            .OrderByDescending(x => x.Sequence).Take(5).ToListAsync();
        var characters = await _db.Characters.Where(x => x.WorkId == workId).ToListAsync();
        var outlines = await _db.Outlines.Where(x => x.WorkId == workId).ToListAsync();

        return new WritingBlackboard
        {
            WorkId = workId,
            RequestId = requestId,
            Meta = new WritingMetaInfo
            {
                Genre = work.Genre,
                Perspective = work.Perspective,
                StyleTags = work.StyleTags,
                TotalWordCount = work.TotalWordCount
            },
            RecentChapters = chapters.Select(c => new ChapterSection
            {
                ChapterId = c.Id, Sequence = c.Sequence,
                Title = c.Title, Content = c.Content, Summary = c.Summary
            }).ToList(),
            Characters = characters.Select(c => new CharacterSection
            {
                CharacterId = c.Id, Name = c.Name, CoreSeed = c.Description
            }).ToList(),
            // ... 更多数据加载
        };
    }
}
```

---

## 四、渐进式披露与创作域工具

核心原则：**禁止将黑板全量内容拼入 SystemPrompt；Agent 通过 Function Calling 按需查询黑板分区。**

### 4.1 设计动机

| 策略 | 一次性注入 | 渐进式披露 |
|------|-----------|----------|
| 上下文来源 | SystemPrompt 内联全量黑板 | Function Calling 按需查询 |
| Token 消耗 | 随小说长度线性增长 | 恒定（仅 Prompt + 按需查询） |
| 信息聚焦 | 噪音多，关键信息被淹没 | LLM 只取需要的分区 |
| 长篇适配 | 超过 context window 即失效 | 任意长度均可工作 |

### 4.2 BlackboardHolder — 黑板桥接器

工具执行时需要访问黑板，但 `IToolExecutor.ExecuteAsync` 只接收 JSON 参数字符串。
通过 Scoped 服务 `BlackboardHolder` 桥接：

```csharp
/// <summary>
/// Scoped 生命周期，持有当前请求的 WritingBlackboard 实例。
/// 创作域工具通过 DI 注入此对象访问黑板数据。
/// </summary>
public sealed class BlackboardHolder
{
    public WritingBlackboard Blackboard { get; set; }
}
```

生命周期：Orchestrator 在构建黑板后设置 `BlackboardHolder.Blackboard`，
工具在 `ToolCapable.ExecuteAsync` 的 `CreateAsyncScope()` 中获取同一 Scoped 实例。

### 4.3 创作域工具定义

每个 Agent 注册自己需要的专用工具，工具内部通过 `BlackboardHolder` 读取黑板分区。

#### WriteAgent 工具集

| 工具名 | 参数 | 数据源 | 说明 |
|--------|------|--------|------|
| `get_world_setting` | `section?` (world_rules/geography/factions/history) | `WorldSetting` | 按分区查询世界观 |
| `get_outline` | `volume_seq?, chapter_seq?` | `Outline` | 查大纲结构，精确到某卷某章 |
| `get_character` | `name` | `Characters` | 查特定角色的完整信息 |
| `search_characters` | `query, limit?` | `Characters` | 模糊搜索角色（按标签/身份） |
| `get_recent_chapters` | `count` | `RecentChapters` | 获取最近 N 章内容 |
| `get_chapter` | `chapter_id` | `RecentChapters` | 获取特定章节 |

#### WorldAgent 工具集

| 工具名 | 参数 | 数据源 | 说明 |
|--------|------|--------|------|
| `get_existing_settings` | `section?` | `WorldSetting` | 查已有世界观分区 |
| `get_characters_in_world` | 无 | `Characters` | 获取所有角色概要（世界观关联） |
| `get_timeline_events` | `era?` | `Meta` | 查历史编年事件 |

#### OutlineAgent 工具集

| 工具名 | 参数 | 数据源 | 说明 |
|--------|------|--------|------|
| `get_world_setting` | 同上 | `WorldSetting` | 大纲需对齐世界观 |
| `get_characters` | 无 | `Characters` | 全量角色（大纲需全局视角） |
| `get_existing_outline` | 无 | `Outline` | 当前大纲结构 |

#### CreationAgent 工具集

| 工具名 | 参数 | 数据源 | 说明 |
|--------|------|--------|------|
| `get_character` | `name` | `Characters` | 查已有角色避免重复 |
| `get_world_setting` | `section?` | `WorldSetting` | 创意需符合世界规则 |
| `get_relationships` | `character_name` | `Characters` | 查角色人际关系 |

#### AuditAgent 工具集

| 工具名 | 参数 | 数据源 | 说明 |
|--------|------|--------|------|
| `get_chapter` | `chapter_id` | `RecentChapters` | 获取待审章节 |
| `get_character` | `name` | `Characters` | 校验角色一致性 |
| `get_world_setting` | `section?` | `WorldSetting` | 校验世界规则 |
| `get_outline` | 无 | `Outline` | 校验情节对齐 |
| `get_foreshadowing` | `status?` | 伏笔表 | 查伏笔回收状态 |

### 4.4 工具实现示例

以 `get_character` 为例，展示创作域工具的标准实现模式：

```csharp
public sealed class GetCharacterTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetCharacterTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition Definition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character",
            Description = "根据角色名称查询角色的完整信息，包括性格、背景、说话风格、成长弧线等",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["name"] = new()
                    {
                        Type = "string",
                        Description = "角色名称"
                    }
                },
                Required = ["name"]
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.Characters == null || board.Characters.Count == 0)
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无角色信息",
                ErrorCode = "no_characters"
            });

        string name = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("name", out var prop))
                name = prop.GetString();
        }
        catch { /* 忽略解析错误 */ }

        if (string.IsNullOrEmpty(name))
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 name 参数",
                ErrorCode = "missing_parameter"
            });

        // 精确匹配 → 模糊匹配
        var character = board.Characters.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? board.Characters.FirstOrDefault(c =>
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (character == null)
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到角色「{name}」，当前作品角色：{string.Join("、", board.Characters.Select(c => c.Name))}",
                ErrorCode = "character_not_found"
            });

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new
            {
                character.Name,
                character.CoreSeed,
                character.Background,
                character.Personality,
                character.Traits,
                character.Voice,
                character.Arc,
                character.Relationships,
                character.Fears,
                character.Desires
            })
        });
    }
}
```

### 4.5 Prompt 调整原则

从「全量注入」转向「引导查询」的 Prompt 编写规范：

1. **角色与规范独立于上下文** — SystemPrompt 只包含角色定义、写作规范、输出要求
2. **信息获取方式显式声明** — 明确列出可用工具及其适用场景，引导 LLM 按需查询
3. **决策原则替代上下文注入** — 用「先查后写、按需查询、一次查准」替代直接塞入数据
4. **禁止在 Prompt 中引用黑板字段** — 所有上下文必须通过工具调用获取

---

## 五、创作编排器（CreationOrchestrator）

Orchestrator 是总控，不负责具体写作，只控制流程。

### 5.1 核心实现

```csharp
public sealed class CreationOrchestrator
{
    private readonly CreationRouter _router;
    private readonly IEnumerable<INovelAgent> _agents;
    private readonly WritingBlackboardBuilder _blackboardBuilder;
    private readonly BlackboardHolder _blackboardHolder; // Scoped：桥接黑板到工具

    public CreationOrchestrator(
        CreationRouter router,
        IEnumerable<INovelAgent> agents,
        WritingBlackboardBuilder blackboardBuilder,
        BlackboardHolder blackboardHolder)
    {
        _router = router;
        _agents = agents;
        _blackboardBuilder = blackboardBuilder;
        _blackboardHolder = blackboardHolder;
    }

    /// <summary>
    /// 流式执行创作请求
    /// </summary>
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId, string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // 1. 路由决策（非流式，瞬时完成）
        var route = await _router.DecideAsync(userMessage, ct);

        // 2. 输出路由元信息给前端
        yield return new AgentStreamChunk
        {
            Type = "meta",
            ContentType = route.ContentType,
            Content = JsonSerializer.Serialize(new
            {
                stage = "routing",
                agent = route.AgentName,
                contentType = route.ContentType,
                reason = route.Reason
            })
        };

        // 3. 构建黑板上下文（非流式）
        yield return new AgentStreamChunk
        {
            Type = "meta",
            ContentType = "system",
            Content = "{\"stage\":\"loading_context\"}"
        };

        var blackboard = await _blackboardBuilder.BuildAsync(workId, Guid.NewGuid().ToString());

        // 4. 将黑板注入 Scoped BlackboardHolder，工具执行时可通过 DI 访问
        _blackboardHolder.Blackboard = blackboard;

        // 5. 执行目标 Agent（流式）
        var agent = _agents.First(a => a.Name == route.AgentName);
        var prompt = agent.BuildPrompt(); // 不再注入黑板，Prompt 独立于上下文

        await foreach (var chunk in agent.ExecuteStreamAsync(
            new AgentRequest
            {
                UserMessage = userMessage,
                SystemPrompt = prompt,
                Model = blackboard.Meta.PreferredModel ?? "gpt-4o",
                MaxIterations = 10
            }, ct))
        {
            yield return chunk;
        }

        // 6. 通知前端完成
        yield return new AgentStreamChunk
        {
            Type = "done",
            Content = "generation_complete"
        };
    }
}
```

### 5.2 Agent 接口

```csharp
/// <summary>
/// 所有小说创作 Agent 的统一接口。
/// 渐进式披露模式：Prompt 独立于黑板上下文，Agent 通过工具按需查询。
/// </summary>
public interface INovelAgent
{
    string Name { get; }                     // write | world | outline | creation | audit
    string DisplayName { get; }              // 写作Agent | 世界观Agent | ...

    /// <summary>
    /// 构建 System Prompt（不含黑板上下文，只含角色定义 + 写作规范 + 工具引导）
    /// </summary>
    string BuildPrompt();

    /// <summary>
    /// 注册该 Agent 专属的创作域工具到 IToolCapable
    /// </summary>
    void RegisterTools(IToolCapable toolCapable);

    /// <summary>
    /// 流式执行（内部使用 ReActAgent）
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        CancellationToken cancellationToken);
}
```

### 5.3 WriteAgent 实现示例

```csharp
public sealed class WriteAgent : INovelAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private bool _toolsInitialized;

    public string Name => "write";
    public string DisplayName => "写作Agent";

    public string BuildPrompt()
    {
        return """
# 角色
你是资深小说写手，擅长各种风格的文字创作。

# 你的能力
- 续写章节
- 润色文字
- 扩写段落
- 重写不满意片段

# 写作规范
- 遵循已建立的世界观设定——如不确定细节，调用 get_world_setting 查询
- 遵循已有大纲路径——如不确定后续走向，调用 get_outline 查看
- 保持人物性格一致性——写涉及某角色时，先调用 get_character 确认其性格和说话风格
- 注意伏笔和前后呼应——参考前文时调用 get_recent_chapters

# 信息获取方式
你拥有一组查询工具，可在写作过程中按需调用：
- 需要世界观规则、地理、势力信息 → 调用 get_world_setting
- 需要大纲结构、章节规划 → 调用 get_outline
- 需要了解某个角色的性格、背景、说话风格 → 调用 get_character
- 需要模糊搜索某类角色 → 调用 search_characters
- 需要回顾前文内容 → 调用 get_recent_chapters
- 需要查看特定章节 → 调用 get_chapter

# 决策原则
1. 先查后写 — 涉及具体设定、角色时，先调用工具确认再动笔
2. 按需查询 — 不需要的信息不要主动查询，节省上下文空间
3. 一次查准 — 尽量精确传参，避免多次查询同类信息

# 输出要求
- 直接输出完整的章节内容，无需输出思考过程
- 每段尽量不超过 300 字
- 注意段落间的过渡自然
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetWorldSettingTool.Definition);
        toolCapable.RegisterTool(GetOutlineTool.Definition);
        toolCapable.RegisterTool(GetCharacterTool.Definition);
        toolCapable.RegisterTool(SearchCharactersTool.Definition);
        toolCapable.RegisterTool(GetRecentChaptersTool.Definition);
        toolCapable.RegisterTool(GetChapterTool.Definition);
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await _llmContext.ResolveAsync(ct);

        request.Model = _llmContext.Model;

        await foreach (var chunk in _react.ExecuteStreamAsync(request, ct))
        {
            yield return chunk;
        }
    }
}
```

### 5.4 AuditAgent 实现示例

```csharp
public sealed class AuditAgent : INovelAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private bool _toolsInitialized;

    public string Name => "audit";
    public string DisplayName => "审核Agent";

    public string BuildPrompt()
    {
        return """
# 角色
你是严格的审稿编辑，擅长发现故事中的逻辑漏洞和一致性问题。

# 你的检查清单
1. □ 人物性格是否前后一致？→ 调用 get_character 核实
2. □ 世界观规则是否被违反？→ 调用 get_world_setting 核实
3. □ 伏笔是否有回收？→ 调用 get_foreshadowing 核实
4. □ 时间线是否有矛盾？→ 调用 get_outline 比对
5. □ 章节之间的衔接是否流畅？→ 调用 get_recent_chapters 对比
6. □ 叙事视角是否统一？→ 检查全文
7. □ 节奏是否有问题？→ 结合大纲判断

# 信息获取方式
你拥有一组查询工具，可在审核过程中按需调用：
- 获取待审章节内容 → 调用 get_chapter
- 核实角色设定 → 调用 get_character
- 核实世界规则 → 调用 get_world_setting
- 比对大纲走向 → 调用 get_outline
- 查伏笔回收状态 → 调用 get_foreshadowing

# 决策原则
1. 先查后判 — 发现疑似问题时，先调用工具确认再下结论
2. 按需查询 — 只查询与当前检查点相关的信息
3. 证据充分 — 每个问题必须引用具体文本作为证据

# 输出要求
- 先给出总体评价（通过/需修改/大改）
- 列出每个问题的严重程度（高/中/低）
- 给出具体修改建议，引用原文
- 如无问题，明确说"通过"
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetChapterTool.Definition);
        toolCapable.RegisterTool(GetCharacterTool.Definition);
        toolCapable.RegisterTool(GetWorldSettingTool.Definition);
        toolCapable.RegisterTool(GetOutlineTool.Definition);
        toolCapable.RegisterTool(GetForeshadowingTool.Definition);
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await _llmContext.ResolveAsync(ct);

        request.Model = _llmContext.Model;

        await foreach (var chunk in _react.ExecuteStreamAsync(request, ct))
        {
            yield return chunk;
        }
    }
}
```

---

## 六、流式协议设计

### 6.1 Chunk 协议

```csharp
public sealed class AgentStreamChunk
{
    public string Type { get; set; }        // meta | content | tool_call | tool_result | done
    public string ContentType { get; set; } // chapter | character | outline | setting | audit_report | plain | system
    public string Content { get; set; }
    public ToolCallDelta ToolCallDelta { get; set; }
    public ToolResult ToolResult { get; set; }
    public AgentResponse FinalResponse { get; set; }
}
```

### 6.2 完整的流式交互示例

```
用户触发: "帮我写第一章，迷雾森林"

↓ SSE 流

← {type: "meta",     contentType: "chapter", content: '{"stage":"routing","agent":"write","reason":"关键词「写」匹配"}'}
← {type: "meta",     contentType: "system",  content: '{"stage":"loading_context"}'}
← {type: "content",  contentType: "chapter", content: "第一章 迷雾森林"}
← {type: "content",  contentType: "chapter", content: "\n\n"}
← {type: "content",  contentType: "chapter", content: "天玄山麓，雾气弥漫。"}
← {type: "content",  contentType: "chapter", content: "清虚道长立于山门之前..."}
← {type: "content",  contentType: "chapter", content: "（持续流式输出...）"}
← {type: "done",     content: "generation_complete"}

前端处理:
  收到 meta(contentType=chapter) → 挂载章节编辑器
  收到 content → 实时填入编辑器正文区
  收到 done → 启用「保存」「继续改」「放弃」按钮
```

### 6.3 前端渲染策略

```
接收 SSE 流
    │
    ▼
第一个 chunk.Type == "meta"
    │
    ├── contentType == "chapter"
    │     → 渲染为「章节编辑器」
    │       └── 后续 content chunk 流式填入编辑器正文区
    │
    ├── contentType == "character"
    │     → 渲染为「角色卡片编辑区」
    │       └── 后续 content 按模板解析字段
    │
    ├── contentType == "outline"
    │     → 渲染为「大纲树」
    │       └── 后续 content 按缩进层级构建树节点
    │
    ├── contentType == "setting"
    │     → 渲染为「设定面板」
    │       └── 后续 content 按 ## 标题分区块展示
    │
    └── contentType == "plain" (兜底)
          → 渲染为「纯文本预览区」
```

---

## 七、世界自生长机制

### 7.1 自生长循环

```
1. WorldAgent 基于当前设定，推导 1-2 个合理的扩展点
2. AuditAgent 检查新扩展是否和已有设定冲突
3. 扩展写回黑板
4. 重复步骤 1，继续下一轮生长
```

### 7.2 WorldAgent 自生长 Prompt

```
# 世界自生长模式

你现在是世界构建专家。你的目标不是一次性生成完整世界观，
而是基于已有设定「生长」出合理的扩展点。

## 已有设定
{current_world_settings}

## 生长方向
选择最有生长潜力的 1-2 个方向：

1. 推导：当前设定自然引出的新设定是什么？
   - "有剑修 → 就该有剑冢、剑试大会、剑道传承"
   - "有炼丹术 → 就该有丹方体系、丹炉、丹药宗门"

2. 填补：当前设定的空白在哪里？
   - "北域有魔族 → 魔修体系、南北对峙格局、边境冲突"

3. 关联：设定之间的矛盾如何调和？
   - "剑修和丹修的关系？竞争还是合作？"

## 输出要求
- 只生长 1-2 个点
- 新设定必须和已有设定逻辑自洽
- 提供「创作提示」说明这个设定怎么用在故事中
```

---

## 八、人物自生长机制

### 8.1 生长维度

```
                      ┌──────────┐
                      │  核心种子  │ ← 用户给的几句话
                      │  (Identity)│
                      ├──────────┤
                      │  背景故事  │ ← CreationAgent 推导过去经历
                      │  (History) │
                      ├──────────┤
                      │  人际关系  │ ← CreationAgent 推导恩怨情仇
                      │(Relations)│
                      ├──────────┤
                      │  性格习惯  │ ← AuditAgent 从行为反推性格
                      │ (Trait)   │
                      ├──────────┤
                      │  成长弧光  │ ← OutlineAgent 人物变化曲线
                      │  (Arc)    │
                      ├──────────┤
                      │  语言风格  │ ← WriteAgent 写作时"代入"
                      │ (Voice)   │
                      └──────────┘
                        不断生长
```

### 8.2 CreationAgent 人物生长 Prompt

```
# 人物自生长模式

你负责让小说人物变得鲜活立体。
你的工作是基于人物已有的信息，自然推导出新的维度。

## 当前人物状态
{character_current_state}

## 生长方向指南
选择最有生长潜力的 1-2 个方向：

1. 矛盾点：他身上有什么矛盾？外表 vs 内心、说 vs 做
2. 缺口：哪段经历是空白的？
3. 关系：他和谁的关系最有趣？
4. 恐惧与欲望：他最想要什么？最怕什么？

## 输出要求
- 只生长 1 个点
- 新生长必须和已有设定一致
- 提供「写作提示」：这个新维度在写作中怎么体现
```

### 8.3 生长触发方式

```csharp
public enum GrowthTrigger
{
    UserRequest,    // 用户主动要求生长
    WritingContext, // 写作中触发生长
    PeriodicReview  // 系统定期触发（每 N 章一次）
}
```

### 8.4 人物生长编排

```csharp
public sealed class CharacterGrowthOrchestrator
{
    private readonly CreationAgent _creationAgent;
    private readonly AuditAgent _auditAgent;
    private readonly WritingBlackboard _blackboard;

    public async Task GrowCharacterAsync(string characterId, GrowthTrigger trigger)
    {
        var character = _blackboard.Characters.First(c => c.CharacterId == characterId);

        // 1. CreationAgent 生长新维度
        var growthResult = await _creationAgent.GrowAsync(character);

        // 2. AuditAgent 检查一致性
        var auditResult = await _auditAgent.CheckCharacterConsistencyAsync(
            character, growthResult);

        if (auditResult.IsConsistent)
        {
            // 3. 写回黑板
            ApplyGrowth(character, growthResult);
            LogGrowthEvent(characterId, growthResult, trigger);
        }
        else
        {
            // 4. 不一致则调整后重试
            await _creationAgent.ReviseAsync(character, growthResult, auditResult);
        }
    }
}
```

---

## 九、数据库保存策略

核心原则：**AI 只负责生成内容预览，保存由用户确认后触发。**

### 9.1 数据流

```
Agent 流式输出 → 前端编辑器展示（临时，不落库）
                      │
                用户满意？
                 ├── ✅ 点「保存」
                 │     → POST /api/chapter/{id}/content
                 │     → ChapterApplication.UpdateChapterAsync()
                 │     → 写入数据库
                 │
                 ├── ✏️ 追加指令
                 │     → 带着已有内容重新触发 Agent
                 │     → 流式输出替换内容
                 │
                 └── ❌ 丢弃
                       → 前端清除，无事发生
```

### 9.2 保存 API（复用现有 Application）

```csharp
// 前端保存时调用——复用 ChapterApplication
[HttpPost("api/works/{workId}/chapters/{chapterId}/content")]
public async Task<ApiResult> SaveChapterContent(
    string workId, string chapterId,
    [FromBody] SaveContentRequest request)
{
    return await _chapterApplication.UpdateChapterAsync(
        workId, chapterId,
        new UpdateChapterRequest { Content = request.Content },
        HttpContext.RequestAborted);
}
```

### 9.3 Function Calling 的合理使用场景

| 场景 | 方式 | 原因 |
|------|------|------|
| 写入最终内容（章节、设定） | Orchestrator 存库 | 确定性强、职责清晰 |
| Agent 需要查询已有数据 | Function Calling | 只有 Agent 自己知道需要查什么 |
| Agent 想保存中间状态/草稿 | Function Calling | 特殊情况，按需提供 |

---

## 十、项目文件对照

| 现有文件 | 职责 | 状态 |
|---------|------|------|
| `Agents/Contract/IWriteAgent.cs` | WriteAgent 接口定义 | 空壳 → 需填充 |
| `Agents/Contract/IWorldAgent.cs` | WorldAgent 接口定义 | 空壳 → 需填充 |
| `Agents/Contract/IOutlineAgent.cs` | OutlineAgent 接口定义 | 空壳 → 需填充 |
| `Agents/Contract/ICreationAgent.cs` | CreationAgent 接口定义 | 空壳 → 需填充 |
| `Agents/Contract/IAuditAgent.cs` | AuditAgent 接口定义 | 空壳 → 需填充 |
| `Agents/WriteAgent.cs` | WriteAgent 实现 | 空壳 → 需填充 |
| `Agents/WorldAgent.cs` | WorldAgent 实现 | 空壳 → 需填充 |
| `Agents/OutlineAgent.cs` | OutlineAgent 实现 | 空壳 → 需填充 |
| `Agents/CreationAgent.cs` | CreationAgent 实现 | 空壳 → 需填充 |
| `Agents/AuditAgent.cs` | AuditAgent 实现 | 空壳 → 需填充 |
| `Context/ICreationAgentContext.cs` | 上下文构建接口 | 已定义 |
| `Context/CreationAgentContext.cs` | 上下文构建实现 | 需实现 |
| `Context/AgentContext.cs` | Agent 上下文模型 | 需扩展为 WritingBlackboard |
| `Memory/IMemoryProvider.cs` | 记忆提供者接口 | 已定义 |
| `Memory/HybridMemoryProvider.cs` | 混合记忆实现 | 空壳 |
| `OpenAIContext.cs` | LLM 配置解析 | 已实现 |

| 新增文件（建议） | 职责 |
|----------------|------|
| `Orchestrator/CreationOrchestrator.cs` | 创作编排器总控 |
| `Orchestrator/CreationRouter.cs` | 路由决策 |
| `Orchestrator/WritingBlackboard.cs` | 黑板数据结构 |
| `Orchestrator/WritingBlackboardBuilder.cs` | 黑板构建器 |
| `Orchestrator/BlackboardHolder.cs` | 黑板桥接器（Scoped） |
| `Contract/INovelAgent.cs` | 统一 Agent 接口（含 BuildPrompt + RegisterTools） |
| `Tools/GetWorldSettingTool.cs` | 世界观查询工具 |
| `Tools/GetCharacterTool.cs` | 角色查询工具 |
| `Tools/SearchCharactersTool.cs` | 角色模糊搜索工具 |
| `Tools/GetOutlineTool.cs` | 大纲查询工具 |
| `Tools/GetRecentChaptersTool.cs` | 最近章节查询工具 |
| `Tools/GetChapterTool.cs` | 特定章节查询工具 |
| `Tools/GetForeshadowingTool.cs` | 伏笔查询工具 |
| `Tools/GetExistingSettingsTool.cs` | 已有世界观查询工具 |
| `Tools/GetCharactersInWorldTool.cs` | 世界观关联角色查询工具 |
| `Tools/GetTimelineEventsTool.cs` | 时间线事件查询工具 |
| `Tools/GetRelationshipsTool.cs` | 角色人际关系查询工具 |

---

## 十一、整体流程回顾

```
用户输入
    │
    ▼
Orchestrator.ExecuteAsync()
    │
    ├── 1. Router.DecideAsync()（非流式）
    │     → 关键词匹配 / LLM 分类
    │     → 返回 RouteResult（Agent + ContentType）
    │     → 发 meta chunk 通知前端准备渲染器
    │
    ├── 2. BlackboardBuilder.BuildAsync()（非流式）
    │     → 加载作品数据
    │     → 构建结构化上下文
    │
    ├── 3. 注入 BlackboardHolder（非流式）
    │     → 将黑板实例设置到 Scoped BlackboardHolder
    │     → 工具执行时可通过 DI 访问黑板数据
    │
    ├── 4. Agent.ExecuteStreamAsync()（流式）
    │     → BuildPrompt() 生成精简角色 Prompt（不含黑板上下文）
    │     → RegisterTools() 注册该 Agent 专属创作域工具
    │     → ReActAgent 执行 LLM 对话 + Function Calling
    │     → Agent 按需调用工具查询黑板分区（渐进式披露）
    │     → content chunk 透传到前端
    │
    ├── 5. Agent 完成 → 发 done chunk
    │
    └── 6. （由前端决定）
          ├── 用户点保存 → 调 API 存库
          ├── 用户追加修改 → 回到步骤 4
          └── 用户放弃 → 结束
```

---

## 十二、关键设计原则

1. **路由不流式，Agent 才流式** — 路由判断瞬时完成，Agent 生成才需要流式
2. **黑板是结构化的** — 不是一个大字符串，而是按领域分区的对象
3. **渐进式披露，禁止全量注入** — 禁止将黑板全量拼入 SystemPrompt，Agent 通过 Function Calling 按需查询黑板分区，先查后写、按需查询、一次查准
4. **存库由前端决定** — AI 只生成预览，确认后才落库
5. **自生长而不是一次性生成** — 世界、人物都是从种子逐步长出来的
6. **Agent 职责单一** — 每个 Agent 只负责一个创作维度，组合起来形成完整系统
7. **复用底层能力** — 所有 Agent 共用 ReActAgent + OpenAIContext + ToolCapable
8. **工具按 Agent 注册** — 每个 Agent 注册自己专属的创作域工具，通过 BlackboardHolder 访问黑板数据
