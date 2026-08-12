using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineVerifyRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    // Baseline verification is a live analysis operation. Keep its explicit build-state contract
    // aligned with validate so framework-dependent consumer assemblies can be loaded from the
    // verified post-build artifact closure rather than the CLI's default load context.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
