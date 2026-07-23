namespace ArchLinterNet.Core.Model;

/// <summary>
/// Canonical, versioned identity of one baseline-eligible violation. Structured fields — never a
/// display message or a source line number — are the unit of baseline identity: one baseline entry
/// suppresses exactly one <see cref="ArchitectureViolationIdentity"/>.
/// </summary>
public sealed record ArchitectureViolationIdentity(
    int IdentityVersion,
    string ContractFamily,
    string Kind,
    string ContractId,
    string? SourceAssembly,
    string SourceType,
    string? SourceMember,
    string? TargetAssembly,
    string? TargetType,
    string? TargetMember,
    int Occurrence,
    string? Configuration = null)
{
    public const int CurrentVersion = 2;

    /// <summary>
    /// Projects this identity down to the legacy version-1 <c>(source_type, forbidden_reference)</c>
    /// pair, for comparing against or generating entries in a legacy baseline document.
    /// </summary>
    public (string SourceType, string ForbiddenReference) ToLegacyPair(string forbiddenReferenceDisplay)
    {
        return (SourceType, forbiddenReferenceDisplay);
    }

    public static string ResolveKind(string contractFamily)
    {
        return contractFamily switch
        {
            "strict" or "audit"
                or "layers" or "allow_only"
                or "cycles" or "acyclic_siblings"
                or "assembly_independence" or "assembly_dependency" or "assembly_allow_only" => "dependency",
            "method_body" or "composition" => "call",
            "package_dependency" or "package_allow_only" => "package",
            "framework_dependency" or "framework_allow_only" => "package",
            "coverage" => "coverage",
            _ => "reference",
        };
    }

    /// <summary>
    /// Strips the <c>strict_</c>/<c>audit_</c> prefix from a baseline contract-group name to recover
    /// the underlying contract family (e.g. <c>strict_method_body</c> → <c>method_body</c>).
    /// </summary>
    public static string ResolveContractFamily(string contractGroup)
    {
        if (contractGroup.StartsWith("strict_", StringComparison.Ordinal))
        {
            return contractGroup["strict_".Length..];
        }

        if (contractGroup.StartsWith("audit_", StringComparison.Ordinal))
        {
            return contractGroup["audit_".Length..];
        }

        return contractGroup;
    }
}
