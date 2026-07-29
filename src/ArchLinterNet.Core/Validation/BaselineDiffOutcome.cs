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

    /// <summary>Every entry in the shared lifecycle vocabulary, for one-shape reporting.</summary>
    public IReadOnlyList<BaselineLifecycleEntry> Entries { get; init; } =
        Array.Empty<BaselineLifecycleEntry>();

    public IReadOnlyList<ArchitectureFinding> Findings => ArchitectureFindingMapper.Order(
        Entries.Select(ArchitectureFindingMapper.FromBaseline));
}
