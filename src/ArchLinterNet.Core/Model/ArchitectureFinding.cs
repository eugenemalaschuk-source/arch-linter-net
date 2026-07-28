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
    public const int CurrentSchemaVersion = 1;

    public string? Mode { get; init; }

    public string? Severity { get; init; }

    public string? BaselineState { get; init; }

    public ArchitecturePolicySourceLocation? PolicyLocation => Details.PolicyLocation;

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations =>
        Details.RelatedPolicyLocations;
}
