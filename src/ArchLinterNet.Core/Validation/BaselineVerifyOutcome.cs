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
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations);
