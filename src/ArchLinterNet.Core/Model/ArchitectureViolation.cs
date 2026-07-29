namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureViolation(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
{
    public IReadOnlyCollection<string>? MatchedNamespacePrefixes { get; init; }

    public IArchitectureDiagnosticPayload? Payload { get; init; }

    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    /// <summary>Authoritative baseline identity when the execution path has already resolved it.</summary>
    public ArchitectureViolationIdentity? Identity { get; init; }

    /// <summary>
    /// Authoritative identities for aggregated violations that contain more than one forbidden
    /// reference. Normalization emits one finding per identity while retaining the legacy grouped
    /// violation for compatibility.
    /// </summary>
    public IReadOnlyCollection<ArchitectureViolationIdentity> Identities { get; init; } =
        Array.Empty<ArchitectureViolationIdentity>();

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations { get; init; } =
        Array.Empty<ArchitecturePolicySourceLocation>();
}
