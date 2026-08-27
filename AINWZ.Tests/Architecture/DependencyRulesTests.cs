using System.Xml.Linq;

namespace AINWZ.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void ApplicationProject_MustNotReferenceInfrastructure()
    {
        var references = ReadProjectReferences("AINWZ.Application/SpeakEase.Write.Application.csproj");

        Assert.DoesNotContain(
            references,
            reference => reference.EndsWith(
                "AINWZ.Infrastructure/SpeakEase.Write.Infrastructure.csproj",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DomainProject_MustNotReferenceBusinessProjects()
    {
        var references = ReadProjectReferences("AINWZ.Domain/SpeakEase.Write.Domain.csproj");

        Assert.Empty(references);
    }

    private static IReadOnlyList<string> ReadProjectReferences(string relativePath)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var projectPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        return XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => (element.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/'))
            .ToList();
    }
}
