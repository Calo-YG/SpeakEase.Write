namespace SpeakEase.AI.Lib.Runtime;

public sealed class PromptCompiler(
    PromptProfileCatalog profiles,
    PromptComposer composer = null)
{
    private readonly PromptProfileCatalog _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly PromptComposer _composer = composer ?? new PromptComposer();

    public string Compile(PromptCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = _profiles.Get(request.ProfileKey) ?? new PromptProfile();
        var effectiveProfile = string.IsNullOrWhiteSpace(request.OutputContract)
            ? profile
            : new PromptProfile
            {
                Identity = profile.Identity,
                Objective = profile.Objective,
                QualityCriteria = profile.QualityCriteria,
                StyleHints = profile.StyleHints,
                OutputContract = request.OutputContract
            };

        return _composer.Compose(effectiveProfile, new PromptCompositionContext
        {
            TaskObjective = request.TaskObjective,
            UserConstraints = request.UserConstraints,
            ContextSummary = request.ContextSummary,
            Capabilities = request.Capabilities
        });
    }
}
