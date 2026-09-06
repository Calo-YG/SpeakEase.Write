namespace SpeakEase.AI.Lib.Runtime;

public sealed class PolicyProfileCatalog
{
    private readonly Dictionary<string, AgentLoopOptions> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string key, AgentLoopOptions options)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Policy profile key is required.", nameof(key));

        ArgumentNullException.ThrowIfNull(options);
        _profiles[key.Trim()] = options;
    }

    public AgentLoopOptions Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return _profiles.GetValueOrDefault(key.Trim());
    }
}
