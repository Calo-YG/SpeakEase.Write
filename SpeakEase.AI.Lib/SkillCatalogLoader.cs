using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib;

public static class SkillCatalogLoader
{
    public static void RegisterFromDirectory(ISkilCapable skills, string directory)
    {
        ArgumentNullException.ThrowIfNull(skills);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories)
                     .Where(path => string.Equals(Path.GetFileName(path), "skill.md", StringComparison.OrdinalIgnoreCase)))
        {
            var metadata = ReadMetadata(path);
            skills.RegiSkill(new SkillDefinition
            {
                Name = metadata.Name,
                Description = metadata.Description,
                Path = Path.GetFullPath(path)
            });
        }
    }

    private static (string Name, string Description) ReadMetadata(string path)
    {
        var name = Path.GetFileName(Path.GetDirectoryName(path)) ?? Path.GetFileNameWithoutExtension(path);
        var description = string.Empty;
        foreach (var line in File.ReadLines(path).Take(32))
        {
            if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var configuredName = line[5..].Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(configuredName))
                    name = configuredName;
            }
            else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                description = line[12..].Trim().Trim('"', '\'');
        }

        return (name, description);
    }
}
