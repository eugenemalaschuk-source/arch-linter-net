using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineRemovedEntry(ArchitectureBaselineComparisonEntry Entry, string RemovalReason);

public sealed record BaselinePruneOutcome(
    bool Succeeded,
    string? Yaml,
    IReadOnlyList<BaselineRemovedEntry> RemovedEntries,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations);
