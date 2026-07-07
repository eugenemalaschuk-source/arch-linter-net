namespace ArchLinterNet.Core.Model;

public sealed record PublicApiSurfaceDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.PublicApiSurface;

    public string? UndeclaredApiSignature { get; init; }
    public bool? ForbiddenPublicConstant { get; init; }
    public string? ApiAssemblyName { get; init; }
    public string? ApiVisibility { get; init; }
}
