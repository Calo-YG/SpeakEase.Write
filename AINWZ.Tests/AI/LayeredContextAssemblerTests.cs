using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Context;

namespace AINWZ.Tests.AI;

public sealed class LayeredContextAssemblerTests
{
    [Fact]
    public void Assemble_TrimsRetrievalThenSessionMemoryAndKeepsCompleteTurns()
    {
        var assembler = new LayeredContextAssembler();
        var result = assembler.Assemble(new LayeredContextAssemblyRequest
        {
            CurrentUserMessage = new string('中', 50),
            ProjectFacts = "角色=林舟",
            SessionMemory = "仍然需要保留的会话摘要",
            RetrievedContext = new string('中', 500),
            ContextWindowTokens = 1_500,
            ConversationTurns = new[]
            {
                Turn(1, new string('中', 50)),
                Turn(2, new string('中', 50))
            }
        });

        Assert.True(result.WasTrimmed);
        Assert.DoesNotContain(result.Messages.OfType<SystemMessage>(), x => x.Content.Contains("[Retrieved Context]"));
        Assert.Contains(result.Messages.OfType<SystemMessage>(), x => x.Content.Contains("[Project Facts]"));
        Assert.Contains(result.Messages.OfType<SystemMessage>(), x => x.Content.Contains("[Session Memory]"));
        Assert.Equal(
            result.Messages.OfType<UserMessage>().Count(),
            result.Messages.OfType<AssistantMessage>().Count());
    }

    [Fact]
    public void Assemble_AccountsForCurrentInputAndReservedOutput()
    {
        var assembler = new LayeredContextAssembler();
        var result = assembler.Assemble(new LayeredContextAssemblyRequest
        {
            CurrentUserMessage = new string('中', 200),
            SessionMemory = new string('中', 2_000),
            ContextWindowTokens = 2_000
        });

        Assert.InRange(result.InputTokenCount, 1, 1_000);
    }

    private static LayeredConversationTurn Turn(int number, string content)
        => new()
        {
            TurnNumber = number,
            Messages = new ChatMessage[]
            {
                ChatMessage.User(content),
                ChatMessage.Assistant(content)
            }
        };
}
