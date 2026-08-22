using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.Validation;

/// <summary>Inputs for one read-only architecture debt gate evaluation.</summary>
public sealed record ArchitectureDebtGateRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public string Mode { get; init; } = "all";

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    /// <summary>Optional base effective-policy context. Must be supplied with <see cref="CurrentPolicyContext"/>.</summary>
    public ArchitecturePolicyContextExport? BasePolicyContext { get; init; }

    /// <summary>Optional current effective-policy context. Must be supplied with <see cref="BasePolicyContext"/>.</summary>
    public ArchitecturePolicyContextExport? CurrentPolicyContext { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    public CancellationToken CancellationToken { get; init; }
}
