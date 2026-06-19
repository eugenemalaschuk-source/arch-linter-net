namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureViolation(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
{
    public string? SourceLayer { get; init; }
    public string? TargetLayer { get; init; }
    public IReadOnlyCollection<string>? AllowedImporters { get; init; }
}
