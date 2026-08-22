using System.Numerics;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

internal sealed class HotspotRawEvidence(
    int commitCount,
    long churn,
    int taskSpread,
    int authorSpread,
    BigInteger temporalSpanSeconds,
    IReadOnlyList<LineCountStatus> lineCountStatuses,
    IReadOnlyList<TaskKey> taskKeys,
    IReadOnlyList<HotspotTaskKeyProvenance> taskKeyProvenance,
    IReadOnlyList<string> canonicalAuthors,
    IReadOnlyList<HotspotAuthorProvenance> authorProvenance)
{
    public int CommitCount { get; } = commitCount;

    public long Churn { get; } = churn;

    public int TaskSpread { get; } = taskSpread;

    public int AuthorSpread { get; } = authorSpread;

    public BigInteger TemporalSpanSeconds { get; } = temporalSpanSeconds;

    public IReadOnlyList<LineCountStatus> LineCountStatuses { get; } = lineCountStatuses;

    public IReadOnlyList<TaskKey> TaskKeys { get; } = taskKeys;

    public IReadOnlyList<HotspotTaskKeyProvenance> TaskKeyProvenance { get; } = taskKeyProvenance;

    public IReadOnlyList<string> CanonicalAuthors { get; } = canonicalAuthors;

    public IReadOnlyList<HotspotAuthorProvenance> AuthorProvenance { get; } = authorProvenance;

    public bool HasBinaryOrUnavailableEvidence => LineCountStatuses.Contains(LineCountStatus.BinaryOrUnavailable);

    public bool HasExactRenameEvidence => LineCountStatuses.Contains(LineCountStatus.ExactRename);

    // V1 creates one baseline identity per exact pathname, so every logical-file finding carries
    // this inherited limitation even where a particular range did not happen to reuse a path.
    public bool PathnameReuseMayConflateGenerations => true;
}

// Each provenance item stays anchored to the canonical file-evidence commit that contributed it.
internal sealed class HotspotTaskKeyProvenance(string commitId, TaskKeyMatch match)
{
    public string CommitId { get; } = commitId;

    public TaskKeyMatch Match { get; } = match;
}

internal sealed class HotspotAuthorProvenance(string commitId, string canonicalAuthor)
{
    public string CommitId { get; } = commitId;

    public string CanonicalAuthor { get; } = canonicalAuthor;
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
    IReadOnlyList<string> aliases,
    IReadOnlyList<FileEvent> pathEvents,
    HistoryPathCategory category,
    HotspotRawEvidence rawEvidence,
    HotspotComponents components,
    HotspotWeights weights,
    decimal score)
{
    public string CanonicalPath { get; } = canonicalPath;

    public IReadOnlyList<string> Aliases { get; } = aliases;

    public IReadOnlyList<FileEvent> PathEvents { get; } = pathEvents;

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
