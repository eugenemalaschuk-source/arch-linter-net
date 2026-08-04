namespace ArchLinterNet.Core.Caching;

// The persisted analysis-cache/v1 envelope. Every field here is either canonical (SchemaId,
// FormatVersion), a digest, or a small closed set of deterministic value types — never a
// polymorphic or arbitrary CLR type, so AnalysisCacheStore never needs unsafe polymorphic
// deserialization to read one back.
public sealed record AnalysisCacheEntryV1
{
    public string SchemaId { get; init; } = AnalysisCacheEnvelope.SchemaId;

    public required int FormatVersion { get; init; }

    public required string KeyDigest { get; init; }

    // The single strict/audit mode this entry's Outcome was evaluated for — see finding #4:
    // one entry per mode, never a combined-mode entry summarizing more than one outcome under a
    // "strict,audit"-shaped key.
    public required string Mode { get; init; }

    public required string ToolVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required AnalysisCacheEntryCompletionStatus CompletionStatus { get; init; }

    public required IReadOnlyList<AnalysisCacheProjectManifest> ProjectManifests { get; init; }

    public required IReadOnlyList<AnalysisCacheArtifactManifest> ArtifactManifests { get; init; }

    public required AnalysisCacheOutcomeV1 Outcome { get; init; }

    // SHA-256 over the canonical form of every field above (see
    // AnalysisCacheContentDigest.Compute) — verified on every read before any other field is
    // trusted.
    public required string ContentDigest { get; init; }
}
