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

    /// <summary>
    /// The forbidden reference each entry of <see cref="Identities"/> was attributed to, aligned
    /// with it by position. Identity attachment already walks the reported references to pick one
    /// candidate per reference, so it is the only place that knows this pairing exactly; recording
    /// it here keeps normalization from having to re-derive it by parsing display text, which
    /// cannot separate two occurrences that differ only by IL offset. Empty when no pairing was
    /// established (violations built outside identity attachment, or the composition family, which
    /// selects candidates without a per-reference walk).
    /// </summary>
    /// <remarks>
    /// Deliberately internal. This is pipeline plumbing between identity attachment and finding
    /// normalization, not part of the diagnostics model callers consume: this record reaches users
    /// through ArchitectureValidationResult.Violations, so a public member here would widen the
    /// package's API surface and appear in any caller's own JSON projection of a raw violation.
    /// Every consumer-facing view of this pairing is the per-finding <c>forbidden_references</c>
    /// that normalization already emits.
    /// </remarks>
    internal IReadOnlyList<string> IdentityReferences { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations { get; init; } =
        Array.Empty<ArchitecturePolicySourceLocation>();
}
