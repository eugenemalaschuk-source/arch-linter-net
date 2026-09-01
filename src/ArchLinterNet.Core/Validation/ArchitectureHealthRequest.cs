namespace ArchLinterNet.Core.Validation;

/// <summary>Inputs for one read-only architecture-health evaluation.</summary>
public sealed record ArchitectureHealthRequest
{
    /// <summary>
    /// The canonical baseline and optional policy-weakening request used for the debt-gate
    /// authority. Health reuses this request rather than defining another policy/baseline shape.
    /// </summary>
    public required ArchitectureDebtGateRequest DebtGate { get; init; }

    /// <summary>Optional workflow identity retained for a later persisted report projection.</summary>
    public string? ExecutionContext { get; init; }
}
