using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class PromptComposerTests
{
    [Fact]
    public void Compose_RendersProfileSectionsInStableOrder()
    {
        var profile = new PromptProfile
        {
            Identity = "你是写作助手。",
            Objective = "完成用户要求的章节任务。",
            QualityCriteria = new[] { "保持人物一致", "尊重已确认设定" },
            StyleHints = new[] { "语言自然" },
            OutputContract = "输出正文。"
        };

        var prompt = new PromptComposer().Compose(profile);

        Assert.Equal(
            "# 身份\n你是写作助手。\n\n# 任务目标\n完成用户要求的章节任务。\n\n# 质量标准\n- 保持人物一致\n- 尊重已确认设定\n\n# 风格提示\n- 语言自然\n\n# 输出契约\n输出正文。",
            prompt);
    }

    [Fact]
    public void Compose_DoesNotRenderEmptySections()
    {
        var prompt = new PromptComposer().Compose(new PromptProfile
        {
            Identity = "通用助手"
        });

        Assert.Equal("# 身份\n通用助手", prompt);
        Assert.DoesNotContain("任务目标", prompt);
        Assert.DoesNotContain("质量标准", prompt);
    }

    [Fact]
    public void Compose_AllowsRequestContextToOverrideOnlyDynamicSections()
    {
        var prompt = new PromptComposer().Compose(
            new PromptProfile { Identity = "助手", Objective = "默认目标" },
            new PromptCompositionContext
            {
                TaskObjective = "本次完成审查",
                UserConstraints = new[] { "只给建议" },
                ContextSummary = "已有章节摘要",
                Capabilities = new[] { "search_outline" }
            });

        Assert.Contains("# 任务目标\n本次完成审查", prompt);
        Assert.DoesNotContain("默认目标", prompt);
        Assert.Contains("# 用户约束\n- 只给建议", prompt);
        Assert.Contains("# 相关上下文\n已有章节摘要", prompt);
        Assert.Contains("# 可用能力\n- search_outline", prompt);
    }
}
