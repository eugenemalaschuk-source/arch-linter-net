namespace ArchLinterNet.Core.Model;

/// <summary>
/// Versioned public envelope for a diagnostic. <see cref="Details"/> is the existing closed
/// <see cref="ArchitectureDiagnostic"/> hierarchy, so callers can pattern match its concrete
/// type instead of recovering family evidence from formatted output.
/// </summary>
public sealed record ArchitectureFinding(
    int SchemaVersion,
    string Kind,
    string ContractName,
    string? ContractId,
    string CanonicalIdentity,
    ArchitectureDiagnostic Details)
{
    public const int CurrentSchemaVersion = 2;

    public string? Mode { get; init; }

    public string? Severity { get; init; }

    public string MessageCode { get; init; } = string.Empty;

    public ArchitecturePolicySourceLocation? PolicyOrigin { get; init; }

    public ArchitectureFindingSourceLocation? SourceLocation { get; init; }

    public string? BaselineState { get; init; }

    /// <summary>
    /// The versioned identity used by baseline matching. Its JSON projection is the wire value of
    /// <see cref="CanonicalIdentity"/>; keeping the structured source alongside the string prevents
    /// adapters from re-deriving a lossy display key.
    /// </summary>
    public ArchitectureViolationIdentity? Identity { get; init; }

    /// <summary>
    /// Optional deterministic remediation guidance. This does not change finding identity and is
    /// never an executable fix or authorization to weaken policy.
    /// </summary>
    public ArchitectureRemediationHint? RemediationHint { get; init; }

    public ArchitecturePolicySourceLocation? PolicyLocation => PolicyOrigin;

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyOrigins { get; init; } =
        Array.Empty<ArchitecturePolicySourceLocation>();

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations => RelatedPolicyOrigins;
}

public sealed record ArchitectureFindingSourceLocation(string Path, int? Line = null, int? Column = null);
