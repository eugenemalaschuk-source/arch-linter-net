using System.Numerics;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

// Immutable, post-ingestion evidence for a coordination-pressure finding. It keeps the pair-level
// details report writers need while deliberately making no claim about an actual merge conflict.
internal sealed class BottleneckTaskInterval(BigInteger startEpochSecond, BigInteger endEpochSecond)
{
    public BigInteger StartEpochSecond { get; } = startEpochSecond;

    public BigInteger EndEpochSecond { get; } = endEpochSecond;
}

internal sealed class BottleneckTaskProvenance(string commitId, TaskKeyMatch match)
{
    public string CommitId { get; } = commitId;

    public TaskKeyMatch Match { get; } = match;
}

internal sealed class BottleneckTaskPair
{
    public required TaskKey First { get; init; }

    public required TaskKey Second { get; init; }

    public required IReadOnlyList<string> FirstExclusiveCommitIds { get; init; }

    public required IReadOnlyList<string> SecondExclusiveCommitIds { get; init; }

    public required IReadOnlyList<BottleneckTaskProvenance> FirstProvenance { get; init; }

    public required IReadOnlyList<BottleneckTaskProvenance> SecondProvenance { get; init; }

    public required BottleneckTaskInterval FirstInterval { get; init; }

    public required BottleneckTaskInterval SecondInterval { get; init; }

    public required BigInteger GapSeconds { get; init; }

    public required BigInteger DaysBetween { get; init; }

    public required decimal TemporalProximity { get; init; }
}

internal sealed class BottleneckRawEvidence
{
    public required int IndependentTaskSpread { get; init; }

    public required int DistinctAuthorCount { get; init; }

    public required decimal IndependentTemporalProximity { get; init; }

    public required int DistinctNeighborDegree { get; init; }

    public required int IncidentCommitDegree { get; init; }

    public required int IncidentTaskDegree { get; init; }

    public required IReadOnlyList<TaskKey> TaskKeys { get; init; }

    public required IReadOnlyList<BottleneckTaskPair> IndependentTaskPairs { get; init; }

    public required IReadOnlyList<string> CanonicalAuthors { get; init; }

    public static bool PathnameReuseMayConflateGenerations => true;
}

internal sealed class BottleneckComponents(
    decimal independentTask,
    decimal author,
    decimal temporal,
    decimal degree,
    decimal incidentCommit,
    decimal incidentTask,
    decimal centrality)
{
    public decimal IndependentTask { get; } = independentTask;

    public decimal Author { get; } = author;

    public decimal Temporal { get; } = temporal;

    public decimal Degree { get; } = degree;

    public decimal IncidentCommit { get; } = incidentCommit;

    public decimal IncidentTask { get; } = incidentTask;

    public decimal Centrality { get; } = centrality;
}

internal sealed class BottleneckWeights(decimal independentTask, decimal author, decimal temporal, decimal degree, decimal centrality)
{
    public decimal IndependentTask { get; } = independentTask;

    public decimal Author { get; } = author;

    public decimal Temporal { get; } = temporal;

    public decimal Degree { get; } = degree;

    public decimal Centrality { get; } = centrality;
}

internal sealed class HistoryBottleneckFinding(
    string canonicalPath,
    IReadOnlyList<string> aliases,
    HistoryPathCategory category,
    BottleneckRawEvidence rawEvidence,
    BottleneckComponents components,
    BottleneckWeights weights,
    decimal score)
{
    public string CanonicalPath { get; } = canonicalPath;

    public IReadOnlyList<string> Aliases { get; } = aliases;

    public HistoryPathCategory Category { get; } = category;

    public BottleneckRawEvidence RawEvidence { get; } = rawEvidence;

    public BottleneckComponents Components { get; } = components;

    public BottleneckWeights Weights { get; } = weights;

    public decimal Score { get; } = score;
}

internal sealed class HistoryBottleneckCategoryGroup(HistoryPathCategory category, IReadOnlyList<HistoryBottleneckFinding> findings)
{
    public HistoryPathCategory Category { get; } = category;

    public IReadOnlyList<HistoryBottleneckFinding> Findings { get; } = findings;
}

internal sealed class HistoryBottleneckAnalysis(IReadOnlyList<HistoryBottleneckCategoryGroup> groups)
{
    public IReadOnlyList<HistoryBottleneckCategoryGroup> Groups { get; } = groups;

    public IReadOnlyList<HistoryBottleneckFinding> GetFindings() => Groups.SelectMany(static group => group.Findings).ToArray();
}
