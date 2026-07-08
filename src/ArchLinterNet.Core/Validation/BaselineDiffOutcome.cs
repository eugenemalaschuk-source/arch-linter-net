using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineDiffOutcome(
    bool Succeeded,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Frozen,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> ConfigurationErrors,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations);
