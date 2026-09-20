using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal sealed record DeferredHotPathEvidenceDocument
{
    public const string SchemaId = "deferred-hot-path-analysis/v1";

    public required string EvidenceSchemaId { get; init; }

    public required string SourceIdentity { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required string Configuration { get; init; }

    public required string ToolIdentity { get; init; }

    public required IReadOnlyList<DeferredHotPathFindingEvidence> Findings { get; init; }
}

internal sealed record DeferredHotPathFindingEvidence
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Hypothesis { get; init; }

    public required string Outcome { get; init; }

    public required string ScaleVariable { get; init; }

    public required string CurrentWorkModel { get; init; }

    public required string ObservedGrowth { get; init; }

    public required string Interpretation { get; init; }

    public required string Routing { get; init; }

    public required IReadOnlyList<DeferredHotPathTopologyEvidence> TopologyEvidence { get; init; }

    public required IReadOnlyList<DeferredHotPathMeasurement> Measurements { get; init; }
}

internal sealed record DeferredHotPathTopologyEvidence
{
    public required string Shape { get; init; }

    public required int ProjectCount { get; init; }

    public required int ReferenceEdgeCount { get; init; }

    public required int StronglyConnectedComponentCount { get; init; }

    public required bool ContainsCycle { get; init; }

    public required string ExecutionStatus { get; init; }

    public required string Reason { get; init; }
}

internal sealed record DeferredHotPathMeasurement
{
    public required string WorkloadId { get; init; }

    public required string Size { get; init; }

    public required string ScaleDimension { get; init; }

    public required int ScaleValue { get; init; }

    public required string ExecutionVariant { get; init; }

    public required int DeterministicWork { get; init; }

    public required string ObservedCounter { get; init; }

    public required int? ObservedCounterValue { get; init; }

    public required string? DominantPhase { get; init; }

    public required double? DominantPhaseMilliseconds { get; init; }

    public required double? SelectorPhaseElapsedMilliseconds { get; init; }

    public required double? SelectorPhaseProcessorTimeMilliseconds { get; init; }

    public required double? SelectorPhaseSharePercent { get; init; }

    public required long? AllocatedBytes { get; init; }

    public required long? PeakWorkingSetBytes { get; init; }

    public required string CanonicalResultSha256 { get; init; }

    public required string CompletionStatus { get; init; }

    public required int ExitCode { get; init; }

    public required JsonElement RawAnalysisProfile { get; init; }
}
