using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

// Immutable, auditable heuristic evidence for OCP pressure. The finding intentionally does not
// claim a formal Open/Closed Principle violation.
internal sealed class OcpTaskRepeatedEdit(
    TaskKey taskKey,
    IReadOnlyList<string> qualifyingCommitIds)
{
    public TaskKey TaskKey { get; } = taskKey;

    public IReadOnlyList<string> QualifyingCommitIds { get; } = qualifyingCommitIds;

    public int RepeatedEditCount => Math.Max(QualifyingCommitIds.Count - 1, 0);
}

internal sealed class OcpRawEvidence(
    int independentTaskSpread,
    int ordinaryTaskKeySpread,
    long churn,
    int commitCount,
    int incidentCommitDegree,
    int incidentTaskDegree,
    int repeatedEditTotal,
    decimal roleHint,
    IReadOnlyList<TaskKey> taskKeys,
    IReadOnlyList<BottleneckTaskPair> independentTaskPairs,
    IReadOnlyList<OcpTaskRepeatedEdit> repeatedEdits,
    IReadOnlyList<string> roleTokens)
{
    public int IndependentTaskSpread { get; } = independentTaskSpread;

    public int OrdinaryTaskKeySpread { get; } = ordinaryTaskKeySpread;

    public long Churn { get; } = churn;

    public int CommitCount { get; } = commitCount;

    public int IncidentCommitDegree { get; } = incidentCommitDegree;

    public int IncidentTaskDegree { get; } = incidentTaskDegree;

    public int RepeatedEditTotal { get; } = repeatedEditTotal;

    public decimal RoleHint { get; } = roleHint;

    public IReadOnlyList<TaskKey> TaskKeys { get; } = taskKeys;

    public IReadOnlyList<BottleneckTaskPair> IndependentTaskPairs { get; } = independentTaskPairs;

    public IReadOnlyList<OcpTaskRepeatedEdit> RepeatedEdits { get; } = repeatedEdits;

    public IReadOnlyList<string> RoleTokens { get; } = roleTokens;

    public bool PathnameReuseMayConflateGenerations => true;
}

internal sealed class OcpComponents(decimal independentTask, decimal centrality, decimal repeatedEdit, decimal roleHint)
{
    public decimal IndependentTask { get; } = independentTask;

    public decimal Centrality { get; } = centrality;

    public decimal RepeatedEdit { get; } = repeatedEdit;

    public decimal RoleHint { get; } = roleHint;
}

internal sealed class OcpWeights(decimal independentTask, decimal centrality, decimal repeatedEdit, decimal roleHint)
{
    public decimal IndependentTask { get; } = independentTask;

    public decimal Centrality { get; } = centrality;

    public decimal RepeatedEdit { get; } = repeatedEdit;

    public decimal RoleHint { get; } = roleHint;
}

internal sealed class HistoryOcpFinding(
    string canonicalPath,
    IReadOnlyList<string> aliases,
    HistoryPathCategory category,
    OcpRawEvidence rawEvidence,
    OcpComponents components,
    OcpWeights weights,
    decimal score)
{
    public string CanonicalPath { get; } = canonicalPath;

    public IReadOnlyList<string> Aliases { get; } = aliases;

    public HistoryPathCategory Category { get; } = category;

    public OcpRawEvidence RawEvidence { get; } = rawEvidence;

    public OcpComponents Components { get; } = components;

    public OcpWeights Weights { get; } = weights;

    public decimal Score { get; } = score;
}

internal sealed class HistoryOcpCategoryGroup(HistoryPathCategory category, IReadOnlyList<HistoryOcpFinding> findings)
{
    public HistoryPathCategory Category { get; } = category;

    public IReadOnlyList<HistoryOcpFinding> Findings { get; } = findings;
}

internal sealed class HistoryOcpAnalysis(IReadOnlyList<HistoryOcpCategoryGroup> groups)
{
    public IReadOnlyList<HistoryOcpCategoryGroup> Groups { get; } = groups;

    public IReadOnlyList<HistoryOcpFinding> GetFindings() => Groups.SelectMany(static group => group.Findings).ToArray();
}
