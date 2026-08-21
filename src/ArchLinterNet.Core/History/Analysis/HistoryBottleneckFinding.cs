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

internal sealed class BottleneckTaskPair(
    TaskKey first,
    TaskKey second,
    IReadOnlyList<string> firstExclusiveCommitIds,
    IReadOnlyList<string> secondExclusiveCommitIds,
    IReadOnlyList<BottleneckTaskProvenance> firstProvenance,
    IReadOnlyList<BottleneckTaskProvenance> secondProvenance,
    BottleneckTaskInterval firstInterval,
    BottleneckTaskInterval secondInterval,
    BigInteger gapSeconds,
    BigInteger daysBetween,
    decimal temporalProximity)
{
    public TaskKey First { get; } = first;

    public TaskKey Second { get; } = second;

    public IReadOnlyList<string> FirstExclusiveCommitIds { get; } = firstExclusiveCommitIds;

    public IReadOnlyList<string> SecondExclusiveCommitIds { get; } = secondExclusiveCommitIds;

    public IReadOnlyList<BottleneckTaskProvenance> FirstProvenance { get; } = firstProvenance;

    public IReadOnlyList<BottleneckTaskProvenance> SecondProvenance { get; } = secondProvenance;

    public BottleneckTaskInterval FirstInterval { get; } = firstInterval;

    public BottleneckTaskInterval SecondInterval { get; } = secondInterval;

    public BigInteger GapSeconds { get; } = gapSeconds;

    public BigInteger DaysBetween { get; } = daysBetween;

    public decimal TemporalProximity { get; } = temporalProximity;
}

internal sealed class BottleneckRawEvidence(
    int independentTaskSpread,
    int distinctAuthorCount,
    decimal independentTemporalProximity,
    int distinctNeighborDegree,
    int incidentCommitDegree,
    int incidentTaskDegree,
    IReadOnlyList<TaskKey> taskKeys,
    IReadOnlyList<BottleneckTaskPair> independentTaskPairs,
    IReadOnlyList<string> canonicalAuthors)
{
    public int IndependentTaskSpread { get; } = independentTaskSpread;

    public int DistinctAuthorCount { get; } = distinctAuthorCount;

    public decimal IndependentTemporalProximity { get; } = independentTemporalProximity;

    public int DistinctNeighborDegree { get; } = distinctNeighborDegree;

    public int IncidentCommitDegree { get; } = incidentCommitDegree;

    public int IncidentTaskDegree { get; } = incidentTaskDegree;

    public IReadOnlyList<TaskKey> TaskKeys { get; } = taskKeys;

    public IReadOnlyList<BottleneckTaskPair> IndependentTaskPairs { get; } = independentTaskPairs;

    public IReadOnlyList<string> CanonicalAuthors { get; } = canonicalAuthors;

    public bool PathnameReuseMayConflateGenerations => true;
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

    public IReadOnlyList<HistoryBottleneckFinding> Findings => Groups.SelectMany(static group => group.Findings).ToArray();
}
