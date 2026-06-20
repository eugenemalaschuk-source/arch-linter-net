namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureUnmatchedIgnoredViolation(
    string ContractName,
    string? ContractId,
    int IgnoreIndex,
    string SourceType,
    string ForbiddenReference,
    string Reason);
