using System.Numerics;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

internal sealed class HotspotRawEvidence
{
    public required int CommitCount { get; init; }

    public required long Churn { get; init; }

    public required int TaskSpread { get; init; }

    public required int AuthorSpread { get; init; }

    public required BigInteger TemporalSpanSeconds { get; init; }

    public required IReadOnlyList<LineCountStatus> LineCountStatuses { get; init; }

    public required IReadOnlyList<TaskKey> TaskKeys { get; init; }

    public required IReadOnlyList<HotspotTaskKeyProvenance> TaskKeyProvenance { get; init; }

    public required IReadOnlyList<string> CanonicalAuthors { get; init; }

    public required IReadOnlyList<HotspotAuthorProvenance> AuthorProvenance { get; init; }

    public bool HasBinaryOrUnavailableEvidence => LineCountStatuses.Contains(LineCountStatus.BinaryOrUnavailable);

    public bool HasExactRenameEvidence => LineCountStatuses.Contains(LineCountStatus.ExactRename);

    // V1 creates one baseline identity per exact pathname, so every logical-file finding carries
    // this inherited limitation even where a particular range did not happen to reuse a path.
    public static bool PathnameReuseMayConflateGenerations => true;
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

internal sealed class HotspotFinding
{
    public required string CanonicalPath { get; init; }

    public required IReadOnlyList<string> Aliases { get; init; }

    public required IReadOnlyList<FileEvent> PathEvents { get; init; }

    public required HistoryPathCategory Category { get; init; }

    public required HotspotRawEvidence RawEvidence { get; init; }

    public required HotspotComponents Components { get; init; }

    public required HotspotWeights Weights { get; init; }

    public required decimal Score { get; init; }
}

internal sealed class HotspotCategoryGroup(HistoryPathCategory category, IReadOnlyList<HotspotFinding> findings)
{
    public HistoryPathCategory Category { get; } = category;

    public IReadOnlyList<HotspotFinding> Findings { get; } = findings;
}

internal sealed class HistoryHotspotAnalysis(IReadOnlyList<HotspotCategoryGroup> groups)
{
    public IReadOnlyList<HotspotCategoryGroup> Groups { get; } = groups;

    public IReadOnlyList<HotspotFinding> GetFindings() => Groups.SelectMany(static group => group.Findings).ToArray();
}
