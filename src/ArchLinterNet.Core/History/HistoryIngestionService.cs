using System.Diagnostics;
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

    public HistoryIngestionOutcome Ingest(
        HistoryIngestionRequest request,
        CancellationToken cancellationToken = default,
        HistoryIngestionTiming? timing = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            timing?.MarkIngestionInvocation();
            return HistoryIngestionOutcome.Success(Run(request, cancellationToken, timing));
        }
        catch (HistoryFailureException exception)
        {
            return HistoryIngestionOutcome.Failure(HistoryFailures.DiagnosticOf(exception));
        }
    }

    private HistoryIngestionResult Run(
        HistoryIngestionRequest request,
        CancellationToken cancellationToken,
        HistoryIngestionTiming? timing)
    {
        Stopwatch? ingestionClock = timing?.Start();
        cancellationToken.ThrowIfCancellationRequested();
        GitRepositoryLayout layout = GitRepositoryLayout.Discover(request.RepositoryPath);
        using GitObjectDatabase objects = new(layout);
        GitCommitReader commitReader = new(objects, layout.DigestLength);
        GitRefResolver refResolver = new(layout, objects);
        GitObjectId from = refResolver.ResolveToCommit(request.AuthoredFrom);
        GitObjectId to = refResolver.ResolveToCommit(request.AuthoredTo);

        cancellationToken.ThrowIfCancellationRequested();
        CommitGraph graph = new(commitReader);
        IReadOnlyList<GitCommit> range = graph.Range(from, to);
        List<CommitEvidence> commits = new(range.Count);
        foreach (GitCommit commit in range)
        {
            cancellationToken.ThrowIfCancellationRequested();
            commits.Add(BuildCommitEvidence(commit));
        }

        GitTreeDiffer differ = new(new GitTreeReader(objects, layout.DigestLength));
        (IReadOnlyList<CommitDelta> deltas, Dictionary<string, List<GitCommit>> addDeleteCommitsByPath) =
            BuildDeltas(range, differ, commitReader, cancellationToken);

        List<RenameCandidate> candidates = [];
        foreach (CommitDelta delta in deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.AddRange(RenameCandidateDetector.Detect(delta.Commit, delta.Changes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RenameComponent> components = new RenameLineageResolver(graph, addDeleteCommitsByPath).Resolve(candidates);
        LogicalFileIdentity identity = new();
        foreach (CommitDelta delta in deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (GitTreeChange change in delta.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                identity.RegisterPath(change.Path);
            }
        }

        foreach (RenameComponent component in components.Where(static component => component.Accepted))
        {
            cancellationToken.ThrowIfCancellationRequested();
            identity.UnionLineage(component.AcceptedSequence);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LogicalFile> files = new FileEvidenceBuilder(objects, identity).Build(deltas, components);
        cancellationToken.ThrowIfCancellationRequested();
        timing?.Record("ingestion", ingestionClock);
        Stopwatch? scoringClock = timing?.Start();
        CoChangeGraph coChangeGraph = new CoChangeGraphBuilder(configuration).Build(files, commits, components);
        cancellationToken.ThrowIfCancellationRequested();
        HistoryBottleneckAnalysis bottleneckAnalysis = HistoryBottleneckScorer.Score(commits, coChangeGraph, configuration);
        cancellationToken.ThrowIfCancellationRequested();
        HistoryOcpAnalysis ocpAnalysis = HistoryOcpScorer.Score(bottleneckAnalysis, coChangeGraph, configuration);
        cancellationToken.ThrowIfCancellationRequested();
        HistoryHotspotAnalysis hotspotAnalysis = HistoryHotspotScorer.Score(commits, files, configuration);
        timing?.Record("scoring", scoringClock);
        candidates.Sort(RenameCandidate.CompareCanonical);
        return new HistoryIngestionResult
        {
            ObjectFormatName = layout.ObjectFormatName,
            AuthoredFrom = request.AuthoredFrom,
            AuthoredTo = request.AuthoredTo,
            ResolvedFrom = from.Hex,
            ResolvedTo = to.Hex,
            Commits = commits,
            ExcludedMergeCount = range.Count(static commit => commit.IsMerge),
            RenameCandidates = candidates,
            RenameComponents = components,
            LogicalFiles = files,
            CoChangeGraph = coChangeGraph,
            BottleneckAnalysis = bottleneckAnalysis,
            OcpAnalysis = ocpAnalysis,
            Configuration = configuration,
            HotspotAnalysis = hotspotAnalysis,
        };
    }

    private CommitEvidence BuildCommitEvidence(GitCommit commit)
    {
        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = taskExtraction.Extract(commit.RawMessage, commit.Id.Hex);
        return new CommitEvidence(commit, commit.Author.CanonicalIdentity(commit.Id.Hex), matches, keys);
    }

    private static (IReadOnlyList<CommitDelta> Deltas, Dictionary<string, List<GitCommit>> AddDeleteCommitsByPath) BuildDeltas(
        IReadOnlyList<GitCommit> range,
        GitTreeDiffer differ,
        GitCommitReader commitReader,
        CancellationToken cancellationToken)
    {
        List<CommitDelta> deltas = [];
        Dictionary<string, List<GitCommit>> addDeleteCommitsByPath = new(StringComparer.Ordinal);
        foreach (GitCommit commit in range.Where(static commit => !commit.IsMerge))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A root commit is diffed against the empty tree, represented by an absent parent tree ID.
            GitObjectId parentTree = commit.Parents.Count == 1 ? commitReader.Read(commit.Parents[0]).Tree : default;
            IReadOnlyList<GitTreeChange> changes = differ.Diff(parentTree, commit.Tree, commit.Id.Hex);
            deltas.Add(new CommitDelta(commit, changes));
            IEnumerable<string> addOrDeletePaths = changes
                .Where(static change => change.Kind is GitTreeChangeKind.Add or GitTreeChangeKind.Delete)
                .Select(static change => change.Path);
            foreach (string path in addOrDeletePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
