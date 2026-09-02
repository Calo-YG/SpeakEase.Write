namespace SpeakEase.AI.Lib.Runtime;

public sealed class PromptProfileCatalog
{
    private readonly Dictionary<string, PromptProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string key, PromptProfile profile)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Prompt profile key is required.", nameof(key));

        ArgumentNullException.ThrowIfNull(profile);
        _profiles[key.Trim()] = profile;
    }

    public PromptProfile Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return _profiles.GetValueOrDefault(key.Trim());
    }
}
