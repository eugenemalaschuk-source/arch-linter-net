using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;

namespace ArchLinterNet.Core.History;

// The successful ingestion result. It exists only after every fail-closed check has passed, which is
// what makes "no partial report" structural rather than a discipline the caller has to remember.
internal sealed class HistoryIngestionResult(
    string objectFormatName,
    string authoredFrom,
    string authoredTo,
    string resolvedFrom,
    string resolvedTo,
    IReadOnlyList<CommitEvidence> commits,
    int excludedMergeCount,
    IReadOnlyList<RenameCandidate> renameCandidates,
    IReadOnlyList<RenameComponent> renameComponents,
    IReadOnlyList<LogicalFile> logicalFiles,
    CoChangeGraph coChangeGraph,
    HistoryBottleneckAnalysis bottleneckAnalysis,
    HistoryOcpAnalysis ocpAnalysis,
    HistoryAnalysisConfiguration? configuration = null,
    HistoryHotspotAnalysis? hotspotAnalysis = null,
    HistoryEnrichmentProjection? enrichment = null)
{
    public string ObjectFormatName { get; } = objectFormatName;

    public string AuthoredFrom { get; } = authoredFrom;

    public string AuthoredTo { get; } = authoredTo;

    public string ResolvedFrom { get; } = resolvedFrom;

    public string ResolvedTo { get; } = resolvedTo;

    public IReadOnlyList<CommitEvidence> Commits { get; } = commits;

    public int ExcludedMergeCount { get; } = excludedMergeCount;

    public IReadOnlyList<RenameCandidate> RenameCandidates { get; } = renameCandidates;

    public IReadOnlyList<RenameComponent> RenameComponents { get; } = renameComponents;

    public IReadOnlyList<LogicalFile> LogicalFiles { get; } = logicalFiles;

    public CoChangeGraph CoChangeGraph { get; } = coChangeGraph;

    public HistoryBottleneckAnalysis BottleneckAnalysis { get; } = bottleneckAnalysis;

    public HistoryOcpAnalysis OcpAnalysis { get; } = ocpAnalysis;

    public HistoryAnalysisConfiguration Configuration { get; } = configuration ?? new HistoryAnalysisConfiguration();

    public HistoryHotspotAnalysis HotspotAnalysis { get; } = hotspotAnalysis ?? new HistoryHotspotAnalysis([]);

    public HistoryEnrichmentProjection Enrichment { get; private set; } = enrichment ?? HistoryEnrichmentProjection.NotRequested;

    internal void ApplyEnrichment(HistoryEnrichmentProjection enrichmentProjection)
    {
        ArgumentNullException.ThrowIfNull(enrichmentProjection);
        Enrichment = enrichmentProjection;
    }
}
