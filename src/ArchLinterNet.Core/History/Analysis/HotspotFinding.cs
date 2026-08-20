using System.Numerics;
using ArchLinterNet.Core.History.Configuration;

namespace ArchLinterNet.Core.History.Analysis;

internal sealed class HotspotRawEvidence(
    int commitCount,
    long churn,
    int taskSpread,
    int authorSpread,
    BigInteger temporalSpanSeconds,
    IReadOnlyList<LineCountStatus> lineCountStatuses)
{
    public int CommitCount { get; } = commitCount;

    public long Churn { get; } = churn;

    public int TaskSpread { get; } = taskSpread;

    public int AuthorSpread { get; } = authorSpread;

    public BigInteger TemporalSpanSeconds { get; } = temporalSpanSeconds;

    public IReadOnlyList<LineCountStatus> LineCountStatuses { get; } = lineCountStatuses;

    public bool HasBinaryOrUnavailableEvidence => LineCountStatuses.Contains(LineCountStatus.BinaryOrUnavailable);

    public bool HasExactRenameEvidence => LineCountStatuses.Contains(LineCountStatus.ExactRename);

    // V1 creates one baseline identity per exact pathname, so every logical-file finding carries
    // this inherited limitation even where a particular range did not happen to reuse a path.
    public bool PathnameReuseMayConflateGenerations => true;
}

internal sealed class HotspotComponents(decimal commit, decimal churn, decimal task, decimal author, decimal temporal)
{
    public decimal Commit { get; } = commit;

    public decimal Churn { get; } = churn;

    public decimal Task { get; } = task;

    public decimal Author { get; } = author;

    public decimal Temporal { get; } = temporal;
}

internal sealed class HotspotWeights(decimal commit, decimal churn, decimal task, decimal author, decimal temporal)
{
    public decimal Commit { get; } = commit;

    public decimal Churn { get; } = churn;

    public decimal Task { get; } = task;

    public decimal Author { get; } = author;

    public decimal Temporal { get; } = temporal;
}

internal sealed class HotspotFinding(
    string canonicalPath,
    HistoryPathCategory category,
    HotspotRawEvidence rawEvidence,
    HotspotComponents components,
    HotspotWeights weights,
    decimal score)
{
    public string CanonicalPath { get; } = canonicalPath;

    public HistoryPathCategory Category { get; } = category;

    public HotspotRawEvidence RawEvidence { get; } = rawEvidence;

    public HotspotComponents Components { get; } = components;

    public HotspotWeights Weights { get; } = weights;

    public decimal Score { get; } = score;
}

internal sealed class HotspotCategoryGroup(HistoryPathCategory category, IReadOnlyList<HotspotFinding> findings)
{
    public HistoryPathCategory Category { get; } = category;

    public IReadOnlyList<HotspotFinding> Findings { get; } = findings;
}

internal sealed class HistoryHotspotAnalysis(IReadOnlyList<HotspotCategoryGroup> groups)
{
    public IReadOnlyList<HotspotCategoryGroup> Groups { get; } = groups;

    public IReadOnlyList<HotspotFinding> Findings => Groups.SelectMany(static group => group.Findings).ToArray();
}
