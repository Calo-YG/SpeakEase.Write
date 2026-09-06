namespace AINWZ.Tests.Architecture;

public sealed class AiInfrastructureBoundaryTests
{
    [Fact]
    public void AiInfrastructure_MustDependOnApplicationDbPort()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var aiDirectory = Path.Combine(repositoryRoot, "AINWZ.Infrastructure", "AI");

        var violations = Directory.EnumerateFiles(aiDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(x => x.line.Contains("SpeakEaseDbContext", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(repositoryRoot, x.path)}:{x.lineNumber}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"AI infrastructure must depend on IWriteDbContext, not SpeakEaseDbContext: {string.Join(", ", violations)}");
    }
}
