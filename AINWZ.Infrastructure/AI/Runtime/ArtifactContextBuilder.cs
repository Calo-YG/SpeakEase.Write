using System.Text;

using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class ArtifactContextBuilder
{
    public string Build(
        string userMessage,
        IReadOnlyList<AgentArtifact> dependencies,
        int dependencyTokenBudget)
    {
        if (dependencies.Count == 0 || dependencyTokenBudget <= 0)
            return userMessage;

        var aggregateBudget = Math.Min(12_000, (int)Math.Floor(dependencyTokenBudget / 1.5));
        if (aggregateBudget <= 0)
            return userMessage;

        if (dependencies.Count == 1)
        {
            const string prefix = "\n\n[Previous agent result]\n";
            var totalBudget = (int)Math.Floor(dependencyTokenBudget / 1.5);
            if (totalBudget <= prefix.Length)
                return userMessage;

            var content = Truncate(dependencies[0].Content, Math.Min(12_000, totalBudget - prefix.Length));
            return $"{userMessage}{prefix}{content}";
        }

        var metadata = dependencies.Select(dependency => new
        {
            Artifact = dependency,
            Prefix = $"\n\n[Dependency artifact: {dependency.StepId}]\nSummary: "
        }).ToList();
        var builder = new StringBuilder(userMessage);
        var remainingBudget = aggregateBudget;
        var remainingDependencies = metadata.Count;
        foreach (var item in metadata)
        {
            if (remainingBudget < item.Prefix.Length)
                break;

            builder.Append(item.Prefix);
            remainingBudget -= item.Prefix.Length;
            var summaryBudget = Math.Max(0, remainingBudget / remainingDependencies);
            var summary = Truncate(item.Artifact.Summary, summaryBudget);
            builder.Append(summary);
            remainingBudget -= summary.Length;
            remainingDependencies--;
        }

        foreach (var item in metadata)
        {
            const string contentLabel = "\nContent:\n";
            if (remainingBudget <= contentLabel.Length || string.IsNullOrWhiteSpace(item.Artifact.Content))
                break;

            var content = Truncate(item.Artifact.Content, remainingBudget - contentLabel.Length);
            if (content.Length == 0)
                continue;
            builder.Append(contentLabel).Append(content);
            remainingBudget -= contentLabel.Length + content.Length;
        }

        return builder.ToString();
    }

    private static string Truncate(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content) || maxChars <= 0)
            return string.Empty;
        return content.Length > maxChars ? content[..maxChars] : content;
    }
}
