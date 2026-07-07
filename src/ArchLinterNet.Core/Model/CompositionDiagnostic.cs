namespace ArchLinterNet.Core.Model;

public sealed record CompositionDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Composition;

    public string? MatchedForbiddenApi { get; init; }
    public string? ExpectedCompositionBoundary { get; init; }
}
