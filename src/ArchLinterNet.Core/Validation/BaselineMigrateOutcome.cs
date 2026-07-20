using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineMigrateEntryReport(
    string ContractGroup,
    string ContractId,
    string SourceType,
    string ForbiddenReference,
    string Status,
    int MatchCount);

public sealed record BaselineMigrateOutcome(
    bool Succeeded,
    string? Yaml,
    int MatchedCount,
    int StaleCount,
    int AmbiguousCount,
    IReadOnlyList<BaselineMigrateEntryReport> Report,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations,
    string? Error = null);
