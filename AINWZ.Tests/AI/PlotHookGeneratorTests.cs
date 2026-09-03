using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Character;

namespace AINWZ.Tests.AI;

public sealed class PlotHookGeneratorTests
{
    [Fact]
    public async Task Generate_UsesUnresolvedGoalsAndConflicts()
    {
        var generator = new PlotHookGenerator();
        var hooks = await generator.GenerateAsync(new CharacterStateSnapshotData
        {
            WorkId = "work-1", CharacterId = "char-1", Version = 4,
            StateJson = "{\"goals\":[\"查明真相\"],\"conflicts\":[\"害怕被利用\"]}"
        });

        Assert.NotEmpty(hooks);
        Assert.All(hooks, hook =>
        {
            Assert.Equal("char-1", hook.CharacterId);
            Assert.Equal(4, hook.StateVersion);
            Assert.False(string.IsNullOrWhiteSpace(hook.Description));
        });
    }
}
