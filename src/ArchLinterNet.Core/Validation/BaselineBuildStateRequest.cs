using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

// Baseline generate/update/prune are live analysis operations. Keep their explicit build-state
// contract aligned with validate and baseline verify/diff -- without it, a policy with explicit
// analysis.target_assemblies and no metric requiring exact artifact binding can never resolve
// those assemblies for a genuinely external target repository.
public abstract record BaselineBuildStateRequest
{
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }
}
