using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Exceptions;

namespace AINWZ.Tests.AI;

public sealed class AgentInputNormalizerTests
{
    [Fact]
    public void Normalize_TrimsFieldsAndNormalizesRoles()
    {
        var request = new AgentChatRequestDto
        {
            WorkId = " work-1 ",
            ClientMessageId = " msg-1 ",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = " USER ", Content = " hello " }
            }
        };

        AgentInputNormalizer.Normalize(request);

        Assert.Equal("work-1", request.WorkId);
        Assert.Equal("msg-1", request.ClientMessageId);
        Assert.Equal("user", request.Messages[0].Role);
        Assert.Equal("hello", request.Messages[0].Content);
    }

    [Fact]
    public void Normalize_RejectsHistoryEndingWithAssistant()
    {
        var request = new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "hello" },
                new() { Role = "assistant", Content = "reply" }
            }
        };

        Assert.Throws<BusinessExceptions>(() => AgentInputNormalizer.Normalize(request));
    }
}
