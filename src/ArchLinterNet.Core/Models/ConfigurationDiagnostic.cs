namespace ArchLinterNet.Core.Model;

public sealed record ConfigurationDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Configuration;

    public string? TemplateName { get; init; }
    public string? ContainerNamespace { get; init; }
    public IReadOnlyCollection<IReadOnlyCollection<string>>? DependencyPaths { get; init; }
}
