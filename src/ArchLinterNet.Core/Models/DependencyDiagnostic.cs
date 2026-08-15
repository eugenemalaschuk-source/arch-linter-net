namespace ArchLinterNet.Core.Model;

public sealed record DependencyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Dependency;

    public string? SourceLayer { get; init; }
    public string? TargetLayer { get; init; }
    public IReadOnlyCollection<string>? AllowedImporters { get; init; }
}
