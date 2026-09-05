namespace ArchLinterNet.Core.Execution;

// Metric-kind calculators return native evidence only. The measurement evaluator owns reason and
// contributor normalization and is the only component that builds applicability or measurement
// models from this evidence.
internal sealed record ArchitectureMetricRawEvidence(
    string Scope,
    string? Unit,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Contributors);
