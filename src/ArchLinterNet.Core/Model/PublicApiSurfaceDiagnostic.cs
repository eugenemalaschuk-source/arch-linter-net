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

    // "added", "removed", or "changed" — the normalized API delta record shared by human, JSON,
    // and SARIF output. Null for contracts that were not evaluated as a delta.
    public string? ApiDeltaKind { get; init; }

    public string? PreviousApiSignature { get; init; }

    // Set when a selected member's signature depends on a first-party exported type (declared in
    // one of the contract's own assemblies) that surface_selector did not itself select.
    public string? UnselectedFirstPartyDependency { get; init; }
}
