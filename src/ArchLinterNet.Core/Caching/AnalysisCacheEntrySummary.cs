namespace ArchLinterNet.Core.Caching;

// `cache inspect` output: deterministic, and deliberately excludes absolute paths — only the
// entry's stable key digest prefix, creation time, and normalized facts are surfaced.
public sealed record AnalysisCacheEntrySummary(
    string EntryFileName,
    bool Readable,
    string? KeyDigest,
    DateTimeOffset? CreatedAtUtc,
    int? ProjectCount,
    bool? Passed);
