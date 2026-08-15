namespace ArchLinterNet.Core.Model;

public sealed record InterfaceImplementationDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.InterfaceImplementation;

    public string? MatchedInterface { get; init; }
    public string? ImplementationKind { get; init; }
    public string? ExpectedImplementationLocation { get; init; }
    public string? ActualImplementationLocation { get; init; }
}
