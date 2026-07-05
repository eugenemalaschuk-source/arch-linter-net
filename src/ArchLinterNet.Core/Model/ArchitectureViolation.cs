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
    public string? ForbiddenExternalGroup { get; init; }
    public string? ForbiddenPackageGroup { get; init; }
    public string? TemplateName { get; init; }
    public string? ContainerNamespace { get; init; }
    public IReadOnlyCollection<IReadOnlyCollection<string>>? DependencyPaths { get; init; }
    public IReadOnlyCollection<string>? MatchedNamespacePrefixes { get; init; }
}
