using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineVerifyOutcome(
    bool Succeeded,
    bool InSync,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Frozen,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> ConfigurationErrors,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations)
{
    /// <summary>
    /// Entries correlating to more than one current candidate. These fail verification: an entry that
    /// suppresses several distinct violations is broadening the ratchet, the same condition
    /// <c>baseline migrate</c> refuses to write through.
    /// </summary>
    public IReadOnlyList<ArchitectureBaselineComparisonEntry> Ambiguous { get; init; } =
        Array.Empty<ArchitectureBaselineComparisonEntry>();

    /// <summary>Every entry in the shared lifecycle vocabulary, for one-shape reporting.</summary>
    public IReadOnlyList<BaselineLifecycleEntry> Entries { get; init; } =
        Array.Empty<BaselineLifecycleEntry>();

    /// <summary>
    /// Build-state failures are distinct from policy configuration violations: the verify gate
    /// did not complete, so hosts can report the typed preflight diagnostics and exit accordingly.
    /// </summary>
    public IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics { get; init; } =
        Array.Empty<BuildStatePreflightDiagnostic>();

    public IReadOnlyList<ArchitectureFinding> Findings => ArchitectureFindingMapper.Order(
        Entries.Select(ArchitectureFindingMapper.FromBaseline));
}
