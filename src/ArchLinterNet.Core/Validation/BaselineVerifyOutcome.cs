using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

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
}
