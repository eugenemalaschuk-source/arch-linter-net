using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Evidence;

// The graph is an immutable projection over canonical ingestion evidence. It deliberately retains
// task-only pairs so the evidence is auditable, while `BaseEdges` alone defines G0 topology.
internal sealed class CoChangeGraph(
    decimal commitWeight,
    decimal taskWeight,
    decimal? significanceThreshold,
    IReadOnlyList<CoChangeVertex> vertices,
    IReadOnlyList<CoChangePair> pairs,
    IReadOnlyList<CoChangePair> baseEdges,
    IReadOnlyList<CoChangeCluster> clusters)
{
    public decimal CommitWeight { get; } = commitWeight;

    public decimal TaskWeight { get; } = taskWeight;

    public decimal? SignificanceThreshold { get; } = significanceThreshold;

    public IReadOnlyList<CoChangeVertex> Vertices { get; } = vertices;

    public IReadOnlyList<CoChangePair> Pairs { get; } = pairs;

    public IReadOnlyList<CoChangePair> BaseEdges { get; } = baseEdges;

    public IReadOnlyList<CoChangeCluster> Clusters { get; } = clusters;
}

internal sealed class CoChangeVertex(
    LogicalFile file,
    HistoryPathCategory category,
    IReadOnlyList<RenameComponent> renameComponents)
{
    public LogicalFile File { get; } = file;

    public HistoryPathCategory Category { get; } = category;

    public IReadOnlyList<RenameComponent> RenameComponents { get; } = renameComponents;

    public string CanonicalPath => File.CanonicalPath;
}

// Endpoint categories are an unordered cohort. The enum declaration order is the canonical category
// order established by the policy specification, so it is safe to use it for cohort ordering.
internal readonly record struct CoChangeCohort(HistoryPathCategory First, HistoryPathCategory Second)
    : IComparable<CoChangeCohort>
{
    public static CoChangeCohort Of(HistoryPathCategory first, HistoryPathCategory second)
        => first <= second ? new CoChangeCohort(first, second) : new CoChangeCohort(second, first);

    public static bool operator <(CoChangeCohort left, CoChangeCohort right) => left.CompareTo(right) < 0;

    public static bool operator <=(CoChangeCohort left, CoChangeCohort right) => left.CompareTo(right) <= 0;

    public static bool operator >(CoChangeCohort left, CoChangeCohort right) => left.CompareTo(right) > 0;

    public static bool operator >=(CoChangeCohort left, CoChangeCohort right) => left.CompareTo(right) >= 0;

    public int CompareTo(CoChangeCohort other)
    {
        int byFirst = First.CompareTo(other.First);
        return byFirst != 0 ? byFirst : Second.CompareTo(other.Second);
    }
}

internal sealed class CoChangePair
{
    public required CoChangeVertex First { get; init; }

    public required CoChangeVertex Second { get; init; }

    public required CoChangeCohort Cohort { get; init; }

    public required IReadOnlyList<string> CommitIds { get; init; }

    public required IReadOnlyList<TaskKey> TaskKeys { get; init; }

    public int CommitCoChange => CommitIds.Count;

    public int TaskCoChange => TaskKeys.Count;

    public bool IsBaseEdge => CommitCoChange > 0;

    // Components apply only to G0 edges. Null prevents task-only associations from masquerading as
    // normalized edges while their raw task count remains inspectable.
    public required decimal? CommitComponent { get; init; }

    public required decimal? TaskComponent { get; init; }

    public required decimal? CombinedCoChange { get; init; }

    // G0 pair ranks begin at one and are meaningful only inside an endpoint-category cohort.
    public required int? CohortRank { get; init; }
}

internal sealed class CoChangeCluster(
    CoChangeCohort cohort,
    IReadOnlyList<CoChangeVertex> members,
    IReadOnlyList<CoChangePair> edges,
    decimal maximum,
    decimal aggregate)
{
    public CoChangeCohort Cohort { get; } = cohort;

    public IReadOnlyList<CoChangeVertex> Members { get; } = members;

    public IReadOnlyList<CoChangePair> Edges { get; } = edges;

    public decimal Maximum { get; } = maximum;

    public decimal Aggregate { get; } = aggregate;
}
