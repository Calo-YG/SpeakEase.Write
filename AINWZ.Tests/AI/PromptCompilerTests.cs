using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class PromptCompilerTests
{
    [Fact]
    public void Compile_UsesTaskContextAndDoesNotEmbedWorkflowScript()
    {
        var catalog = new PromptProfileCatalog();
        catalog.Register("novel.writer", new PromptProfile
        {
            Identity = "你是小说正文创作助手。",
            QualityCriteria = new[] { "保持人物和世界观一致" },
            OutputContract = "输出正文，不输出内部推理。"
        });
        var compiler = new PromptCompiler(catalog);

        var prompt = compiler.Compile(new PromptCompileRequest
        {
            ProfileKey = "novel.writer",
            TaskObjective = "完成当前章节正文",
            UserConstraints = new[] { "保持第三人称视角" },
            ContextSummary = "当前位于第二卷",
            Capabilities = new[] { "chapter.read", "chapter.write" },
            OutputContract = "输出章节正文"
        });

        Assert.Contains("完成当前章节正文", prompt);
        Assert.Contains("chapter.read", prompt);
        Assert.DoesNotContain("必须先调用", prompt);
        Assert.DoesNotContain("Thought", prompt);
    }

    [Fact]
    public void Compile_UnknownProfile_UsesSafeMinimalProfile()
    {
        var compiler = new PromptCompiler(new PromptProfileCatalog());

        var prompt = compiler.Compile(new PromptCompileRequest
        {
            ProfileKey = "missing",
            TaskObjective = "回答用户问题"
        });

        Assert.Contains("回答用户问题", prompt);
        Assert.DoesNotContain("missing", prompt);
    }
}
