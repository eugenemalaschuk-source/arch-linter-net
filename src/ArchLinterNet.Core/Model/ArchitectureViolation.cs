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
    public string? ExpectedTypeLocation { get; init; }
    public string? ActualTypeLocation { get; init; }
    public string? ExpectedTypeName { get; init; }
    public string? ActualTypeName { get; init; }
    public string? UndeclaredApiSignature { get; init; }
    public bool? ForbiddenPublicConstant { get; init; }
    public string? ApiAssemblyName { get; init; }
    public string? ApiVisibility { get; init; }
    public string? MatchedAttribute { get; init; }
    public string? AttributeUsageKind { get; init; }
    public string? ExpectedAttributeLocation { get; init; }
    public string? ActualAttributeLocation { get; init; }
}
