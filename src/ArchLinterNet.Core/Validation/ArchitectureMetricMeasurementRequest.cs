using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

/// <summary>Inputs for one read-only Core metric measurement operation.</summary>
public sealed record ArchitectureMetricMeasurementRequest
{
    public required string PolicyPath { get; init; }

    public IReadOnlyCollection<string>? MetricIds { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    public int? MaxParallelism { get; init; }

    public CancellationToken CancellationToken { get; init; }

    internal AnalysisSnapshotRequest ToSnapshotRequest() => new()
    {
        PolicyPath = PolicyPath,
        ConditionSetName = ConditionSetName,
        PreprocessorSymbols = PreprocessorSymbols,
        IncludeAsmdefContracts = IncludeAsmdefContracts,
        PreparationMode = PreparationMode,
        RequestedConfiguration = RequestedConfiguration,
        RequestedTargetFramework = RequestedTargetFramework,
        RequestedPlatform = RequestedPlatform,
        RequestedRuntimeIdentifier = RequestedRuntimeIdentifier,
        MaxParallelism = MaxParallelism,
        CancellationToken = CancellationToken,
        IsMetricMeasurement = true,
    };
}
