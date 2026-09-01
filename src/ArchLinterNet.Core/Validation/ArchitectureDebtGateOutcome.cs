using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;

namespace ArchLinterNet.Core.Validation;

/// <summary>Evaluation receipt for the complete current architecture analysis used by the gate.</summary>
public sealed record ArchitectureDebtGateEvaluation(
    bool Completed,
    string Mode,
    IReadOnlyList<BuildStatePreflightDiagnostic> PreflightDiagnostics)
{
    /// <summary>
    /// Whether persistent-debt comparison used the candidate receipt from the caller's existing
    /// analysis snapshot instead of collecting candidates through a second analysis path.
    /// </summary>
    public bool ReusedAnalysisSnapshot { get; init; }
}

/// <summary>One normalized, read-only architecture debt gate result.</summary>
public sealed record ArchitectureDebtGateOutcome(
    bool Succeeded,
    bool Passed,
    ArchitectureDebtGateEvaluation Evaluation,
    BaselineVerifyOutcome PersistentDebt)
{
    /// <summary>
    /// Optional change-time guardrail result. It is intentionally distinct from
    /// <see cref="PersistentDebt"/> and never receives baseline lifecycle status.
    /// </summary>
    public ArchitecturePolicyWeakeningResult? PolicyWeakening { get; init; }

    public bool PolicyWeakeningRequested { get; init; }
}
