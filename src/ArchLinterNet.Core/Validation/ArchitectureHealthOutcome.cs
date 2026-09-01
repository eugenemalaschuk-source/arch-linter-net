using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>One current validation receipt included in an architecture-health projection.</summary>
public sealed record ArchitectureHealthValidationOutcome(string Mode, ValidationOutcome Outcome);

/// <summary>Complete Core-owned result for one read-only architecture-health evaluation.</summary>
public sealed record ArchitectureHealthOutcome(
    ArchitectureHealthSummary Summary,
    IReadOnlyList<ArchitectureHealthValidationOutcome> ValidationOutcomes,
    ArchitectureDebtGateOutcome DebtGate)
{
    public ArchitectureHealthGate Gate => Summary.Gate;

    public ArchitectureHealthState Health => Summary.Health;

    /// <summary>
    /// Counters from the immutable analysis snapshot that supplied both validation and baseline
    /// candidate receipts. They make the one-snapshot Health orchestration observable.
    /// </summary>
    public ArchitectureAnalysisSnapshotCounters AnalysisCounters { get; init; } = new();

    /// <summary>Workflow identity supplied with the request, when the caller persisted one.</summary>
    public string? ExecutionContext { get; init; }

    /// <summary>Condition set used by the shared immutable Health analysis snapshot.</summary>
    public string ConditionSetName { get; init; } = string.Empty;
}
