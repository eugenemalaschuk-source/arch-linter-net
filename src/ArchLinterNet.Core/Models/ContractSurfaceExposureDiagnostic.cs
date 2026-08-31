namespace ArchLinterNet.Core.Model;

/// <summary>Path-rich diagnostic for one forbidden visible-contract exposure.</summary>
public sealed record ContractSurfaceExposureDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ContractSurfaceExposure;

    public string? SourceAssemblyName { get; init; }

    public string? DeclaringSourceType { get; init; }

    public string? ExposurePath { get; init; }

    public string? CanonicalExposurePath { get; init; }

    public string? TargetAssemblyName { get; init; }

    public string? TargetTypeName { get; init; }

    public string? SourceSurface { get; init; }

    public string? MemberOrMetadataSite { get; init; }

    public string? ReviewedPublicApiSurface { get; init; }

    public IReadOnlyCollection<int>? MatchingForbiddenSelectors { get; init; }
}
