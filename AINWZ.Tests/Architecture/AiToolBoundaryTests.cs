namespace AINWZ.Tests.Architecture;

public sealed class AiToolBoundaryTests
{
    [Fact]
    public void AiTools_MustNotReferenceConcreteDbContext()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var toolsDirectory = Path.Combine(
            repositoryRoot,
            "AINWZ.Infrastructure",
            "AI",
            "Tools");

        var violations = Directory.EnumerateFiles(toolsDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(x => x.line.Contains("SpeakEaseDbContext", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(repositoryRoot, x.path)}:{x.lineNumber}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"AI tools must depend on Application ports, not SpeakEaseDbContext: {string.Join(", ", violations)}");
    }
}
