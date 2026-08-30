namespace ArchLinterNet.Core.Model;

/// <summary>Explicit producer/CI metadata associated with one external evidence artifact.</summary>
/// <remarks>
/// These values are deliberately opaque. The reader compares them exactly and never derives them
/// from an artifact name, timestamp, workflow, or another filesystem property.
/// </remarks>
public sealed record SarifEvidenceProducerContext
{
    /// <summary>Creates an empty producer context.</summary>
    public SarifEvidenceProducerContext()
    {
    }

    /// <summary>Creates producer metadata for repository, revision, and scope.</summary>
    public SarifEvidenceProducerContext(string? repository, string? revision, string? scope)
    {
        Repository = NormalizeOptional(repository);
        Revision = NormalizeOptional(revision);
        Scope = NormalizeOptional(scope);
    }

    /// <summary>Creates producer metadata including an explicitly supplied logical identity.</summary>
    public SarifEvidenceProducerContext(
        string? logicalId,
        string? repository,
        string? revision,
        string? scope)
        : this(repository, revision, scope)
    {
        LogicalId = NormalizeOptional(logicalId);
    }

    /// <summary>The optional logical evidence identity supplied by the producer.</summary>
    public string? LogicalId { get; init; }

    /// <summary>The optional repository identity supplied by the producer or CI.</summary>
    public string? Repository { get; init; }

    /// <summary>The optional source revision supplied by the producer or CI.</summary>
    public string? Revision { get; init; }

    /// <summary>The optional assessment scope supplied by the producer or CI.</summary>
    public string? Scope { get; init; }

    /// <summary>Alias for the standard SARIF repository vocabulary.</summary>
    public string? RepositoryUri => Repository;

    /// <summary>Alias for the standard SARIF revision vocabulary.</summary>
    public string? RevisionId => Revision;

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>One explicitly declared local artifact bound to a logical evidence requirement.</summary>
public sealed record SarifEvidenceArtifactReference
{
    /// <summary>Creates an artifact reference.</summary>
    public SarifEvidenceArtifactReference(
        string path,
        string logicalId,
        SarifEvidenceProducerContext? producerContext = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("An evidence artifact path is required.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(logicalId))
        {
            throw new ArgumentException("An evidence logical identity is required.", nameof(logicalId));
        }

        Path = path;
        LogicalId = logicalId;
        ProducerContext = producerContext;
    }

    /// <summary>The supplied path, which must resolve to a repository-local regular file.</summary>
    public string Path { get; }

    /// <summary>Logical identity explicitly bound to the supplied artifact.</summary>
    public string LogicalId { get; }

    /// <summary>Optional producer/CI metadata supplied alongside the artifact.</summary>
    public SarifEvidenceProducerContext? ProducerContext { get; }

    /// <summary>Alias emphasizing that this is a repository-relative input path.</summary>
    public string ArtifactPath => Path;
}

/// <summary>Current assessment context against which a SARIF artifact is bound.</summary>
public sealed record SarifEvidenceAssessmentContext
{
    /// <summary>Creates an assessment context. Unset dimensions remain null.</summary>
    public SarifEvidenceAssessmentContext(
        string? repository = null,
        string? revision = null,
        string? scope = null)
    {
        Repository = NormalizeOptional(repository);
        Revision = NormalizeOptional(revision);
        Scope = NormalizeOptional(scope);
    }

    /// <summary>The current repository identity, when known.</summary>
    public string? Repository { get; }

    /// <summary>The current source revision, when known.</summary>
    public string? Revision { get; }

    /// <summary>The current logical assessment scope, when known.</summary>
    public string? Scope { get; }

    /// <summary>Alias for the standard SARIF repository vocabulary.</summary>
    public string? RepositoryUri => Repository;

    /// <summary>Alias for the standard SARIF revision vocabulary.</summary>
    public string? RevisionId => Revision;

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>Deterministic resource limits for one bounded SARIF read.</summary>
public sealed record SarifEvidenceLimits
{
    /// <summary>Creates limits for artifact bytes, runs, and selected-run results.</summary>
    public SarifEvidenceLimits(
        long maxArtifactBytes = 4 * 1024 * 1024,
        int maxRuns = 32,
        int maxResults = 100_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxArtifactBytes);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRuns);

        ArgumentOutOfRangeException.ThrowIfNegative(maxResults);

        MaxArtifactBytes = maxArtifactBytes;
        MaxRuns = maxRuns;
        MaxResults = maxResults;
    }

    /// <summary>Maximum number of artifact bytes that may be consumed.</summary>
    public long MaxArtifactBytes { get; }

    /// <summary>Maximum number of runs accepted in one SARIF document.</summary>
    public int MaxRuns { get; }

    /// <summary>Maximum number of results accepted in the selected run.</summary>
    public int MaxResults { get; }

    /// <summary>Alias for consumers that use the long-form limit vocabulary.</summary>
    public long MaximumArtifactBytes => MaxArtifactBytes;

    /// <summary>Alias for consumers that use the long-form limit vocabulary.</summary>
    public int MaximumRuns => MaxRuns;

    /// <summary>Alias for consumers that use the long-form limit vocabulary.</summary>
    public int MaximumResults => MaxResults;
}

/// <summary>Closed trust state returned by the bounded SARIF reader.</summary>
public enum SarifEvidenceTrustStatus
{
    Valid,
    OptionalNotConfigured,
    MissingRequiredInput,
    MissingOptionalInput,
    UnreadableInput,
    UnsafePath,
    ArtifactTooLarge,
    MalformedInput,
    UnsupportedVersion,
    UnsupportedShape,
    MissingExpectedRun,
    AmbiguousExpectedRun,
    FailedExecution,
    IncompleteExecution,
    MissingLogicalId,
    WrongLogicalId,
    MissingRepository,
    WrongRepository,
    MissingRevision,
    WrongRevision,
    MissingScope,
    WrongScope,
    ConflictingContext,
    TooManyRuns,
    TooManyResults,
}

/// <summary>Conservatively merged context retained from SARIF and explicit producer metadata.</summary>
public sealed record SarifEvidenceResolvedContext(
    string? LogicalId,
    string? Repository,
    string? Revision,
    string? Scope)
{
    /// <summary>Alias for the standard SARIF repository vocabulary.</summary>
    public string? RepositoryUri => Repository;

    /// <summary>Alias for the standard SARIF revision vocabulary.</summary>
    public string? RevisionId => Revision;
}

/// <summary>Deterministic provenance retained for downstream evidence selection and findings.</summary>
public sealed record SarifEvidenceProvenance(
    string LogicalId,
    string? ArtifactPath,
    string? ArtifactSha256,
    string? ToolName,
    string? ToolVersion,
    string? RunId,
    int? ResultCount,
    SarifEvidenceResolvedContext? Context)
{
    /// <summary>Alias for the selected SARIF driver name.</summary>
    public string? SelectedToolName => ToolName;

    /// <summary>Alias for the selected SARIF driver version.</summary>
    public string? SelectedToolVersion => ToolVersion;

    /// <summary>Alias for the selected SARIF automation run id.</summary>
    public string? SelectedRunId => RunId;

    /// <summary>Alias for the canonical lowercase content digest.</summary>
    public string? ArtifactHash => ArtifactSha256;
}

/// <summary>Closed result of one bounded local SARIF trust read.</summary>
public sealed record SarifEvidenceReadResult
{
    internal SarifEvidenceReadResult(
        SarifEvidenceTrustStatus status,
        string reasonCode,
        string detail,
        SarifEvidenceProvenance provenance,
        IReadOnlyList<SarifEvidenceSourceDiagnostic>? sourceDiagnostics = null,
        SarifEvidenceAuthorizationSnapshot? authorization = null)
    {
        Status = status;
        ReasonCode = reasonCode;
        Detail = detail;
        Provenance = provenance;
        SourceDiagnostics = sourceDiagnostics is null || sourceDiagnostics.Count == 0
            ? Array.Empty<SarifEvidenceSourceDiagnostic>()
            : Array.AsReadOnly(sourceDiagnostics.ToArray());
        Authorization = authorization;
    }

    /// <summary>The single trust decision for this artifact.</summary>
    public SarifEvidenceTrustStatus Status { get; }

    /// <summary>Alias emphasizing that status is the trust decision, not a diagnostic outcome.</summary>
    public SarifEvidenceTrustStatus TrustStatus => Status;

    /// <summary>Stable machine-readable reason suitable for applicability mapping.</summary>
    public string ReasonCode { get; }

    /// <summary>Actionable, non-authoritative detail for diagnostics.</summary>
    public string Detail { get; }

    /// <summary>All identity, context, and byte provenance retained by the reader.</summary>
    public SarifEvidenceProvenance Provenance { get; }

    /// <summary>
    /// Immutable typed source diagnostics from the selected run. This collection is non-empty only
    /// for a valid trusted read; trust failures never expose selectable source facts.
    /// </summary>
    public IReadOnlyList<SarifEvidenceSourceDiagnostic> SourceDiagnostics { get; }

    /// <summary>
    /// Immutable policy and assessment authorization captured when this result was trusted. It is
    /// present only for valid reads and prevents a caller from selecting the evidence under a
    /// different mutable policy requirement.
    /// </summary>
    public SarifEvidenceAuthorizationSnapshot? Authorization { get; }

    /// <summary>Whether the selected run is trusted evidence.</summary>
    public bool IsValid => Status == SarifEvidenceTrustStatus.Valid;

    /// <summary>Logical identity of the configured requirement.</summary>
    public string LogicalId => Provenance.LogicalId;

    /// <summary>Normalized repository-relative path, when bytes were read.</summary>
    public string? ArtifactPath => Provenance.ArtifactPath;

    /// <summary>Alias for the normalized repository-relative path.</summary>
    public string? ArtifactRelativePath => ArtifactPath;

    /// <summary>Lowercase SHA-256 of bytes consumed, when bytes were read.</summary>
    public string? ArtifactSha256 => Provenance.ArtifactSha256;

    /// <summary>Alias for the lowercase artifact SHA-256.</summary>
    public string? Sha256 => ArtifactSha256;

    /// <summary>Selected SARIF driver name, when a matching run was found.</summary>
    public string? ToolName => Provenance.ToolName;

    /// <summary>Alias for the selected SARIF driver name.</summary>
    public string? SelectedToolName => ToolName;

    /// <summary>Selected SARIF driver version, when present.</summary>
    public string? ToolVersion => Provenance.ToolVersion;

    /// <summary>Alias for the selected SARIF driver version.</summary>
    public string? SelectedToolVersion => ToolVersion;

    /// <summary>Selected SARIF automation run id, when a matching run was found.</summary>
    public string? RunId => Provenance.RunId;

    /// <summary>Alias for the selected SARIF automation run id.</summary>
    public string? SelectedRunId => RunId;

    /// <summary>Selected run result count, when a matching run was found.</summary>
    public int? ResultCount => Provenance.ResultCount;

    /// <summary>Alias for the selected run result count.</summary>
    public int? SelectedResultCount => ResultCount;

    /// <summary>Merged and validated repository, revision, scope, and logical identity.</summary>
    public SarifEvidenceResolvedContext? Context => Provenance.Context;

    /// <summary>Alias for the merged and validated context.</summary>
    public SarifEvidenceResolvedContext? ResolvedContext => Context;
}
