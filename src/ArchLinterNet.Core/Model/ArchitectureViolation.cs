namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureViolation(
    string ContractName,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences);
