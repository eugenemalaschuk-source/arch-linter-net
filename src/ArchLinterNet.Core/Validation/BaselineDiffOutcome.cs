using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineDiffOutcome(
    bool Succeeded,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Frozen,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> ConfigurationErrors,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations)
{
    public IReadOnlyList<ArchitectureBaselineComparisonEntry> Ambiguous { get; init; } =
        Array.Empty<ArchitectureBaselineComparisonEntry>();

    // Populated when build-state preflight blocks baseline collection, so callers that require
    // a complete result can present the exact failure rather than treating debt as empty.
    public IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics { get; init; } =
        Array.Empty<BuildStatePreflightDiagnostic>();

    /// <summary>Every entry in the shared lifecycle vocabulary, for one-shape reporting.</summary>
    public IReadOnlyList<BaselineLifecycleEntry> Entries { get; init; } =
        Array.Empty<BaselineLifecycleEntry>();

    public IReadOnlyList<ArchitectureFinding> Findings => ArchitectureFindingMapper.Order(
        Entries.Select(ArchitectureFindingMapper.FromBaseline));
}
