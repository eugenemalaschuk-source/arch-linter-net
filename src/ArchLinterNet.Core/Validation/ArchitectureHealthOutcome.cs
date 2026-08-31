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
}
