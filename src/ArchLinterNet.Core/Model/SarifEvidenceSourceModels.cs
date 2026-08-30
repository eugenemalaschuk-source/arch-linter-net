using System.Collections.Generic;

namespace ArchLinterNet.Core.Model;

/// <summary>The source severity declared by a SARIF result.</summary>
/// <remarks>
/// SARIF omits <c>level</c> in some producer output. Such a result is represented explicitly as
/// <see cref="Unspecified"/> rather than being silently assigned a vendor or ArchLinterNet level.
/// </remarks>
public enum SarifEvidenceSourceSeverity
{
    Error,
    Warning,
    Note,
    None,
    Unspecified,
}

/// <summary>A source region carried by a SARIF physical location.</summary>
public sealed record SarifEvidenceSourceRegion
{
    /// <summary>Creates a source region. Unspecified coordinates remain null.</summary>
    public SarifEvidenceSourceRegion(
        int? startLine = null,
        int? startColumn = null,
        int? endLine = null,
        int? endColumn = null,
        int? charOffset = null,
        int? charLength = null)
    {
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
        CharOffset = charOffset;
        CharLength = charLength;
    }

    /// <summary>One-based starting line, when supplied by the source.</summary>
    public int? StartLine { get; }

    /// <summary>One-based starting column, when supplied by the source.</summary>
    public int? StartColumn { get; }

    /// <summary>One-based ending line, when supplied by the source.</summary>
    public int? EndLine { get; }

    /// <summary>One-based ending column, when supplied by the source.</summary>
    public int? EndColumn { get; }

    /// <summary>Zero-based character offset, when supplied by the source.</summary>
    public int? CharOffset { get; }

    /// <summary>Character length, when supplied by the source.</summary>
    public int? CharLength { get; }

}

/// <summary>The primary repository-relative source location of a SARIF result.</summary>
public sealed record SarifEvidenceSourceLocation
{
    /// <summary>Creates a source location with an optional SARIF region.</summary>
    public SarifEvidenceSourceLocation(string? path, SarifEvidenceSourceRegion? region = null)
    {
        Path = path;
        Region = region;
    }

    /// <summary>Normalized repository-relative source path, when the source supplied one.</summary>
    public string? Path { get; }

    /// <summary>Source region, when the source supplied one.</summary>
    public SarifEvidenceSourceRegion? Region { get; }

}

/// <summary>One string-valued source fingerprint pair from a SARIF result.</summary>
public sealed record SarifEvidenceSourceFingerprint
{
    /// <summary>Creates a fingerprint pair.</summary>
    public SarifEvidenceSourceFingerprint(string name, string value, bool isPartial = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A source fingerprint name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
        IsPartial = isPartial;
    }

    /// <summary>The source-provided fingerprint map key.</summary>
    public string Name { get; }

    /// <summary>The source-provided fingerprint map value.</summary>
    public string Value { get; }

    /// <summary>Whether this pair came from SARIF <c>partialFingerprints</c>.</summary>
    public bool IsPartial { get; }

}

/// <summary>Typed source facts retained from one trusted SARIF result.</summary>
public sealed record SarifEvidenceSourceDiagnostic
{
    /// <summary>Creates a source diagnostic from immutable source facts.</summary>
    public SarifEvidenceSourceDiagnostic(
        string? message,
        string? ruleId,
        SarifEvidenceSourceSeverity sourceSeverity,
        SarifEvidenceSourceLocation? primaryLocation = null,
        string? project = null,
        IReadOnlyList<string>? driverRuleTags = null,
        IReadOnlyList<SarifEvidenceSourceFingerprint>? fingerprints = null,
        IReadOnlyList<SarifEvidenceSourceFingerprint>? partialFingerprints = null)
    {
        Message = message;
        RuleId = ruleId;
        SourceSeverity = sourceSeverity;
        PrimaryLocation = primaryLocation;
        Project = project;
        DriverRuleTags = ReadOnlyCopy(driverRuleTags);
        Fingerprints = ReadOnlyCopy(fingerprints);
        PartialFingerprints = ReadOnlyCopy(partialFingerprints);

        List<SarifEvidenceSourceFingerprint> allFingerprints =
            new(Fingerprints.Count + PartialFingerprints.Count);
        allFingerprints.AddRange(Fingerprints);
        allFingerprints.AddRange(PartialFingerprints);
        FingerprintPairs = ReadOnlyCopy(allFingerprints);
    }

    /// <summary>The original SARIF result message text, when supplied.</summary>
    public string? Message { get; }

    /// <summary>The original SARIF result rule identifier, when supplied.</summary>
    public string? RuleId { get; }

    /// <summary>The original SARIF source severity, including explicit unspecified handling.</summary>
    public SarifEvidenceSourceSeverity SourceSeverity { get; }

    /// <summary>The result's primary repository-relative source location, when supplied.</summary>
    public SarifEvidenceSourceLocation? PrimaryLocation { get; }

    /// <summary>The optional source project identity.</summary>
    public string? Project { get; }

    /// <summary>Tags attached to the matching SARIF driver rule by exact rule id.</summary>
    public IReadOnlyList<string> DriverRuleTags { get; }

    /// <summary>String-valued SARIF <c>fingerprints</c> pairs in source order.</summary>
    public IReadOnlyList<SarifEvidenceSourceFingerprint> Fingerprints { get; }

    /// <summary>String-valued SARIF <c>partialFingerprints</c> pairs in source order.</summary>
    public IReadOnlyList<SarifEvidenceSourceFingerprint> PartialFingerprints { get; }

    /// <summary>All source fingerprint pairs, full pairs followed by partial pairs.</summary>
    public IReadOnlyList<SarifEvidenceSourceFingerprint> FingerprintPairs { get; }

    private static IReadOnlyList<T> ReadOnlyCopy<T>(IReadOnlyList<T>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<T>();
        }

        T[] copy = new T[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return Array.AsReadOnly(copy);
    }
}
