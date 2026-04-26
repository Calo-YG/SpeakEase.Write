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

## 四、创作编排器（CreationOrchestrator）

Orchestrator 是总控，不负责具体写作，只控制流程。

### 4.1 核心实现

```csharp
public sealed class CreationOrchestrator
{
    private readonly CreationRouter _router;
    private readonly IEnumerable<INovelAgent> _agents;
    private readonly WritingBlackboardBuilder _blackboardBuilder;
    private readonly ISSEForwardProvider _sse;

    public CreationOrchestrator(
        CreationRouter router,
        IEnumerable<INovelAgent> agents,
        WritingBlackboardBuilder blackboardBuilder,
        ISSEForwardProvider sse)
    {
        _router = router;
        _agents = agents;
        _blackboardBuilder = blackboardBuilder;
        _sse = sse;
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

        // 4. 执行目标 Agent（流式）
        var agent = _agents.First(a => a.Name == route.AgentName);
        var prompt = agent.BuildPrompt(blackboard);

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

        // 5. 通知前端完成
        yield return new AgentStreamChunk
        {
            Type = "done",
            Content = "generation_complete"
        };
    }
}
```

### 4.2 Agent 接口

```csharp
/// <summary>
/// 所有小说创作 Agent 的统一接口
/// </summary>
public interface INovelAgent
{
    string Name { get; }                     // write | world | outline | creation | audit
    string DisplayName { get; }              // 写作Agent | 世界观Agent | ...

    /// <summary>
    /// 基于黑板上下文构建 System Prompt
    /// </summary>
    string BuildPrompt(WritingBlackboard blackboard);

    /// <summary>
    /// 流式执行（内部使用 ReActAgent）
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        CancellationToken cancellationToken);
}
```

### 4.3 WriteAgent 实现示例

```csharp
public sealed class WriteAgent : INovelAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;

    public string Name => "write";
    public string DisplayName => "写作Agent";

    public string BuildPrompt(WritingBlackboard blackboard)
    {
        return $"""
# 角色
你是资深小说写手，擅长各种风格的文字创作。

# 你的能力
- 续写章节
- 润色文字
- 扩写段落
- 重写不满意片段

# 写作规范
- 遵循已建立的世界观设定
- 遵循已有大纲路径
- 保持人物性格一致性
- 注意伏笔和前后呼应

# 当前作品上下文

## 世界观设定
{blackboard.WorldSetting?.WorldRules ?? "（暂无）"}

## 大纲结构
{FormatOutline(blackboard.Outline)}

## 人物信息
{FormatCharacters(blackboard.Characters)}

## 已写内容摘要
{blackboard.RecentChapters?.LastOrDefault()?.Summary ?? "（暂无）"}

## 当前章节前文
{blackboard.RecentChapters?.LastOrDefault()?.Content ?? "（暂无）"}

# 输出要求
- 输出完整的章节内容
- 每段尽量不超过 300 字
- 注意段落间的过渡自然
- 如需要更多上下文，通过工具查询
""";
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

    private static string FormatOutline(OutlineSection outline)
    {
        if (outline?.Volumes == null) return "（暂无大纲）";
        return string.Join("\n", outline.Volumes.Select(v =>
            $"- 卷 {v.Sequence}: {v.Title} ({v.Chapters.Count} 章)"));
    }

    private static string FormatCharacters(List<CharacterSection> characters)
    {
        if (characters?.Any() != true) return "（暂无人物）";
        return string.Join("\n", characters.Select(c =>
            $"- {c.Name}: {c.Personality ?? "待补充"}"));
    }
}
```

### 4.4 AuditAgent 实现示例

```csharp
public sealed class AuditAgent : INovelAgent
{
    public string Name => "audit";
    public string DisplayName => "审核Agent";

    public string BuildPrompt(WritingBlackboard blackboard)
    {
        return $"""
# 角色
你是严格的审稿编辑，擅长发现故事中的逻辑漏洞和一致性问题。

# 你的检查清单
1. □ 人物性格是否前后一致？
2. □ 世界观规则是否被违反？
3. □ 伏笔是否有回收？
4. □ 时间线是否有矛盾？
5. □ 章节之间的衔接是否流畅？
6. □ 叙事视角是否统一？
7. □ 节奏是否有问题？

# 检查对象
## 待检查章节
{blackboard.RecentChapters?.LastOrDefault()?.Content ?? "（暂无内容）"}

## 已有上下文
{FormatContext(blackboard)}

# 输出要求
- 先给出总体评价（通过/需修改/大改）
- 列出每个问题的严重程度（高/中/低）
- 给出具体修改建议
- 如无问题，明确说"通过"
""";
    }

    private static string FormatContext(WritingBlackboard board)
    {
        var parts = new List<string>();
        if (board.Characters?.Any() == true)
            parts.Add($"人物数: {board.Characters.Count}");
        if (board.WorldSetting != null)
            parts.Add("世界观: 已设定");
        if (board.Outline != null)
            parts.Add("大纲: 已规划");
        return string.Join(" | ", parts);
    }
}
```

---

## 五、流式协议设计

### 5.1 Chunk 协议

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

### 5.2 完整的流式交互示例

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

### 5.3 前端渲染策略

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

## 六、世界自生长机制

### 6.1 自生长循环

```
1. WorldAgent 基于当前设定，推导 1-2 个合理的扩展点
2. AuditAgent 检查新扩展是否和已有设定冲突
3. 扩展写回黑板
4. 重复步骤 1，继续下一轮生长
```

### 6.2 WorldAgent 自生长 Prompt

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

## 七、人物自生长机制

### 7.1 生长维度

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

### 7.2 CreationAgent 人物生长 Prompt

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

### 7.3 生长触发方式

```csharp
public enum GrowthTrigger
{
    UserRequest,    // 用户主动要求生长
    WritingContext, // 写作中触发生长
    PeriodicReview  // 系统定期触发（每 N 章一次）
}
```

### 7.4 人物生长编排

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

## 八、数据库保存策略

核心原则：**AI 只负责生成内容预览，保存由用户确认后触发。**

### 8.1 数据流

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

### 8.2 保存 API（复用现有 Application）

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

### 8.3 Function Calling 的合理使用场景

| 场景 | 方式 | 原因 |
|------|------|------|
| 写入最终内容（章节、设定） | Orchestrator 存库 | 确定性强、职责清晰 |
| Agent 需要查询已有数据 | Function Calling | 只有 Agent 自己知道需要查什么 |
| Agent 想保存中间状态/草稿 | Function Calling | 特殊情况，按需提供 |

---

## 九、项目文件对照

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
| `Orchestrator/INovelAgent.cs` | 统一 Agent 接口 |

---

## 十、整体流程回顾

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
    ├── 3. Agent.ExecuteStreamAsync()（流式）
    │     → BuildPrompt(blackboard) 注入上下文
    │     → ReActAgent 执行 LLM 对话
    │     → content chunk 透传到前端
    │
    ├── 4. Agent 完成 → 发 done chunk
    │
    └── 5. （由前端决定）
          ├── 用户点保存 → 调 API 存库
          ├── 用户追加修改 → 回到步骤 3
          └── 用户放弃 → 结束
```

---

## 十一、关键设计原则

1. **路由不流式，Agent 才流式** — 路由判断瞬时完成，Agent 生成才需要流式
2. **黑板是结构化的** — 不是一个大字符串，而是按领域分区的对象
3. **存库由前端决定** — AI 只生成预览，确认后才落库
4. **自生长而不是一次性生成** — 世界、人物都是从种子逐步长出来的
5. **Agent 职责单一** — 每个 Agent 只负责一个创作维度，组合起来形成完整系统
6. **复用底层能力** — 所有 Agent 共用 ReActAgent + OpenAIContext + SSEForwardProvider
