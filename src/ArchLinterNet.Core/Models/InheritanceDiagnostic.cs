namespace ArchLinterNet.Core.Model;

public sealed record InheritanceDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Inheritance;

    public string? ForbiddenBaseType { get; init; }
    public string? InheritanceSourceSurface { get; init; }
}
