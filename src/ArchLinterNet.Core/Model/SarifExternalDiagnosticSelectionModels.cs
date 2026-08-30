using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Model;

/// <summary>One trusted evidence result together with the policy requirement that authorizes it.</summary>
public sealed record SarifExternalDiagnosticSelectionInput
{
    /// <summary>Creates one authorized trusted-evidence selection input.</summary>
    public SarifExternalDiagnosticSelectionInput(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceReadResult evidence)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(evidence);
        Requirement = requirement;
        Evidence = evidence;
    }

    /// <summary>The exact logical evidence and diagnostic-filter policy requirement.</summary>
    public ArchitectureExternalEvidenceRequirement Requirement { get; }

    /// <summary>The already trust-validated source evidence result.</summary>
    public SarifEvidenceReadResult Evidence { get; }
}

/// <summary>The ArchLinterNet governance mode mapped from one original source severity.</summary>
public enum SarifExternalDiagnosticGovernanceMode
{
    Strict,
    Audit,
}

/// <summary>The source of a selected external-diagnostic fingerprint.</summary>
public enum SarifExternalDiagnosticFingerprintOrigin
{
    Source,
    Deterministic,
}

/// <summary>One preferred source or deterministic fallback fingerprint.</summary>
public sealed record SarifExternalDiagnosticFingerprint
{
    /// <summary>Creates the selected diagnostic fingerprint.</summary>
    public SarifExternalDiagnosticFingerprint(
        SarifExternalDiagnosticFingerprintOrigin origin,
        string value,
        string? sourceName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A diagnostic fingerprint value is required.", nameof(value));
        }

        Origin = origin;
        Value = value;
        SourceName = sourceName;
    }

    /// <summary>Whether the source supplied the fingerprint or the selector derived it.</summary>
    public SarifExternalDiagnosticFingerprintOrigin Origin { get; }

    /// <summary>The source value or deterministic lowercase SHA-256 value.</summary>
    public string Value { get; }

    /// <summary>The source fingerprint key when <see cref="Origin"/> is <see cref="SarifExternalDiagnosticFingerprintOrigin.Source"/>.</summary>
    public string? SourceName { get; }
}

/// <summary>The selector category that required a configured value to match.</summary>
public enum SarifExternalDiagnosticFilterDimension
{
    RuleId,
    RuleTag,
    Project,
    PathPrefix,
    Severity,
}

/// <summary>Explicit evidence that one required configured filter value matched no trusted source result.</summary>
public sealed record SarifExternalDiagnosticFilterMismatch(
    string LogicalEvidenceId,
    SarifExternalDiagnosticFilterDimension Dimension,
    string Value);

/// <summary>One deterministic, policy-selected external source diagnostic.</summary>
public sealed record SarifSelectedExternalDiagnostic
{
    /// <summary>Creates the selected diagnostic with immutable authorizing provenance.</summary>
    public SarifSelectedExternalDiagnostic(
        string canonicalIdentity,
        SarifEvidenceSourceDiagnostic sourceDiagnostic,
        SarifExternalDiagnosticGovernanceMode governanceMode,
        SarifExternalDiagnosticFingerprint fingerprint,
        IReadOnlyList<SarifEvidenceProvenance> evidenceProvenances)
    {
        if (string.IsNullOrWhiteSpace(canonicalIdentity))
        {
            throw new ArgumentException("A canonical selected-diagnostic identity is required.", nameof(canonicalIdentity));
        }

        ArgumentNullException.ThrowIfNull(sourceDiagnostic);
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(evidenceProvenances);
        CanonicalIdentity = canonicalIdentity;
        SourceDiagnostic = sourceDiagnostic;
        GovernanceMode = governanceMode;
        Fingerprint = fingerprint;
        EvidenceProvenances = Array.AsReadOnly(evidenceProvenances.ToArray());
    }

    /// <summary>Stable semantic identity independent of source display text and input enumeration.</summary>
    public string CanonicalIdentity { get; }

    /// <summary>Original typed source diagnostic facts retained from the trusted SARIF run.</summary>
    public SarifEvidenceSourceDiagnostic SourceDiagnostic { get; }

    /// <summary>The policy-mapped strict or audit governance mode.</summary>
    public SarifExternalDiagnosticGovernanceMode GovernanceMode { get; }

    /// <summary>The selected source or deterministic fallback fingerprint.</summary>
    public SarifExternalDiagnosticFingerprint Fingerprint { get; }

    /// <summary>Ordered #520 provenance for every equivalent source occurrence that was deduplicated.</summary>
    public IReadOnlyList<SarifEvidenceProvenance> EvidenceProvenances { get; }

}

/// <summary>The deterministic selected diagnostics and explicit required-filter mismatch evidence.</summary>
public sealed record SarifExternalDiagnosticSelectionResult
{
    /// <summary>Creates an immutable selection result.</summary>
    public SarifExternalDiagnosticSelectionResult(
        IReadOnlyList<SarifSelectedExternalDiagnostic>? diagnostics = null,
        IReadOnlyList<SarifExternalDiagnosticFilterMismatch>? filterMismatches = null)
    {
        Diagnostics = diagnostics is null || diagnostics.Count == 0
            ? Array.Empty<SarifSelectedExternalDiagnostic>()
            : Array.AsReadOnly(diagnostics.ToArray());
        FilterMismatches = filterMismatches is null || filterMismatches.Count == 0
            ? Array.Empty<SarifExternalDiagnosticFilterMismatch>()
            : Array.AsReadOnly(filterMismatches.ToArray());
    }

    /// <summary>Canonical selected diagnostics ordered by their canonical identities.</summary>
    public IReadOnlyList<SarifSelectedExternalDiagnostic> Diagnostics { get; }

    /// <summary>Ordered filter values that were explicitly required but matched no trusted source result.</summary>
    public IReadOnlyList<SarifExternalDiagnosticFilterMismatch> FilterMismatches { get; }

    /// <summary>Whether every explicitly required configured filter value matched.</summary>
    public bool HasRequiredFilterMatches => FilterMismatches.Count == 0;
}
