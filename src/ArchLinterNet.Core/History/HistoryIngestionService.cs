using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History;

// The canonical ingestion pipeline entry point. Everything downstream of the authored operands is
// derived from raw repository objects, and any fail-closed condition unwinds to a diagnostic before
// a result object exists.
internal sealed class HistoryIngestionService(
    TaskKeyExtraction taskExtraction,
    HistoryAnalysisConfiguration configuration)
{
    public static HistoryIngestionService Default { get; } = new(TaskKeyExtraction.Default, new HistoryAnalysisConfiguration());

    public HistoryIngestionOutcome Ingest(HistoryIngestionRequest request)
    {
        try
        {
            return HistoryIngestionOutcome.Success(Run(request));
        }
        catch (HistoryFailureException exception)
        {
            return HistoryIngestionOutcome.Failure(HistoryFailures.DiagnosticOf(exception));
        }
    }

    private HistoryIngestionResult Run(HistoryIngestionRequest request)
    {
        GitRepositoryLayout layout = GitRepositoryLayout.Discover(request.RepositoryPath);
        using GitObjectDatabase objects = new(layout);
        GitCommitReader commitReader = new(objects, layout.DigestLength);
        GitRefResolver refResolver = new(layout, objects);
        GitObjectId from = refResolver.ResolveToCommit(request.AuthoredFrom);
        GitObjectId to = refResolver.ResolveToCommit(request.AuthoredTo);

        CommitGraph graph = new(commitReader);
        IReadOnlyList<GitCommit> range = graph.Range(from, to);
        IReadOnlyList<CommitEvidence> commits = [.. range.Select(BuildCommitEvidence)];

        GitTreeDiffer differ = new(new GitTreeReader(objects, layout.DigestLength));
        (IReadOnlyList<CommitDelta> deltas, Dictionary<string, List<GitCommit>> addDeleteCommitsByPath) = BuildDeltas(range, differ, commitReader);

        List<RenameCandidate> candidates = [];
        foreach (CommitDelta delta in deltas)
        {
            candidates.AddRange(RenameCandidateDetector.Detect(delta.Commit, delta.Changes));
        }

        IReadOnlyList<RenameComponent> components = new RenameLineageResolver(graph, addDeleteCommitsByPath).Resolve(candidates);
        LogicalFileIdentity identity = new();
        foreach (CommitDelta delta in deltas)
        {
            foreach (GitTreeChange change in delta.Changes)
            {
                identity.RegisterPath(change.Path);
            }
        }

        foreach (RenameComponent component in components.Where(static component => component.Accepted))
        {
            identity.UnionLineage(component.AcceptedSequence);
        }

        IReadOnlyList<LogicalFile> files = new FileEvidenceBuilder(objects, identity).Build(deltas, components);
        CoChangeGraph coChangeGraph = new CoChangeGraphBuilder(configuration).Build(files, commits, components);
        HistoryBottleneckAnalysis bottleneckAnalysis = new HistoryBottleneckScorer().Score(commits, coChangeGraph, configuration);
        HistoryOcpAnalysis ocpAnalysis = new HistoryOcpScorer().Score(bottleneckAnalysis, coChangeGraph, configuration);
        HistoryHotspotAnalysis hotspotAnalysis = new HistoryHotspotScorer().Score(commits, files, configuration);
        candidates.Sort(RenameCandidate.CompareCanonical);
        return new HistoryIngestionResult(
            layout.ObjectFormatName,
            request.AuthoredFrom,
            request.AuthoredTo,
            from.Hex,
            to.Hex,
            commits,
            range.Count(static commit => commit.IsMerge),
            candidates,
            components,
            files,
            coChangeGraph,
            bottleneckAnalysis,
            ocpAnalysis,
            configuration,
            hotspotAnalysis,
            HistoryEnrichmentProjection.NotRequested);
    }

    private CommitEvidence BuildCommitEvidence(GitCommit commit)
    {
        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = taskExtraction.Extract(commit.RawMessage, commit.Id.Hex);
        return new CommitEvidence(commit, commit.Author.CanonicalIdentity(commit.Id.Hex), matches, keys);
    }

    private static (IReadOnlyList<CommitDelta> Deltas, Dictionary<string, List<GitCommit>> AddDeleteCommitsByPath) BuildDeltas(
        IReadOnlyList<GitCommit> range,
        GitTreeDiffer differ,
        GitCommitReader commitReader)
    {
        List<CommitDelta> deltas = [];
        Dictionary<string, List<GitCommit>> addDeleteCommitsByPath = new(StringComparer.Ordinal);
        foreach (GitCommit commit in range.Where(static commit => !commit.IsMerge))
        {
            // A root commit is diffed against the empty tree, represented by an absent parent tree ID.
            GitObjectId parentTree = commit.Parents.Count == 1 ? commitReader.Read(commit.Parents[0]).Tree : default;
            IReadOnlyList<GitTreeChange> changes = differ.Diff(parentTree, commit.Tree, commit.Id.Hex);
            deltas.Add(new CommitDelta(commit, changes));
            IEnumerable<string> addOrDeletePaths = changes
                .Where(static change => change.Kind is GitTreeChangeKind.Add or GitTreeChangeKind.Delete)
                .Select(static change => change.Path);
            foreach (string path in addOrDeletePaths)
            {
                if (!addDeleteCommitsByPath.TryGetValue(path, out List<GitCommit>? touching))
                {
                    touching = [];
                    addDeleteCommitsByPath[path] = touching;
                }

                touching.Add(commit);
            }
        }

        return (deltas, addDeleteCommitsByPath);
    }
}
