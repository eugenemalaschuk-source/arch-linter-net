namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureBaselineComparisonEntry(
    string ContractGroup,
    string ContractId,
    string SourceType,
    string ForbiddenReference,
    string? Reason);

public sealed record ArchitectureBaselineComparisonResult(
    IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Frozen,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> ConfigurationErrors);
