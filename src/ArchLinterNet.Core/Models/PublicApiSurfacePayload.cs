namespace ArchLinterNet.Core.Model;

public sealed record PublicApiSurfacePayload(
    string? UndeclaredApiSignature = null,
    bool? ForbiddenPublicConstant = null,
    string? ApiAssemblyName = null,
    string? ApiVisibility = null,
    string? ApiDeltaKind = null,
    string? PreviousApiSignature = null)
    : IArchitectureDiagnosticPayload
{
    // A body-declared init property, not a positional primary-constructor parameter: this record's
    // constructor and Deconstruct are part of the reviewed public API surface (see #94/#525's own
    // self-governance), and a positional record's arity is a binary contract — adding a 7th
    // positional parameter would replace, not overload, the existing 6-parameter constructor and
    // Deconstruct, breaking every precompiled caller of either (PR #529 review).
    public string? UnselectedFirstPartyDependency { get; init; }

    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new PublicApiSurfaceDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            UndeclaredApiSignature = UndeclaredApiSignature,
            ForbiddenPublicConstant = ForbiddenPublicConstant,
            ApiAssemblyName = ApiAssemblyName,
            ApiVisibility = ApiVisibility,
            ApiDeltaKind = ApiDeltaKind,
            PreviousApiSignature = PreviousApiSignature,
            UnselectedFirstPartyDependency = UnselectedFirstPartyDependency
        };
}
