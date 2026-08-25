using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;

namespace ArchLinterNet.Core.History;

// The successful ingestion result. It exists only after every fail-closed check has passed, which is
// what makes "no partial report" structural rather than a discipline the caller has to remember.
internal sealed class HistoryIngestionResult
{
    public required string ObjectFormatName { get; init; }

    public required string AuthoredFrom { get; init; }

    public required string AuthoredTo { get; init; }

    public required string ResolvedFrom { get; init; }

    public required string ResolvedTo { get; init; }

    public required IReadOnlyList<CommitEvidence> Commits { get; init; }

    public required int ExcludedMergeCount { get; init; }

    public required IReadOnlyList<RenameCandidate> RenameCandidates { get; init; }

    public required IReadOnlyList<RenameComponent> RenameComponents { get; init; }

    public required IReadOnlyList<LogicalFile> LogicalFiles { get; init; }

    public required CoChangeGraph CoChangeGraph { get; init; }

    public required HistoryBottleneckAnalysis BottleneckAnalysis { get; init; }

    public required HistoryOcpAnalysis OcpAnalysis { get; init; }

    public HistoryAnalysisConfiguration Configuration { get; init; } = new HistoryAnalysisConfiguration();

    public HistoryHotspotAnalysis HotspotAnalysis { get; init; } = new HistoryHotspotAnalysis([]);

    public HistoryEnrichmentProjection Enrichment { get; private set; } = HistoryEnrichmentProjection.NotRequested;

    internal void ApplyEnrichment(HistoryEnrichmentProjection enrichmentProjection)
    {
        ArgumentNullException.ThrowIfNull(enrichmentProjection);
        Enrichment = enrichmentProjection;
    }
}
