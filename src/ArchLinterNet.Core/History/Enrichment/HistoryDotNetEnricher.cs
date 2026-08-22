using ArchLinterNet.Core.History.Analysis;

namespace ArchLinterNet.Core.History.Enrichment;

internal sealed class HistoryDotNetEnricher(IHistoryDotNetFactProvider? factProvider = null)
{
    private readonly IHistoryDotNetFactProvider _factProvider = factProvider ?? new WorktreeHistoryDotNetFactProvider();

    public HistoryDotNetEnrichment Enrich(HistoryIngestionResult result, HistoryIngestionRequest request, string? policyPath)
    {
        if (!request.RequestDotNetEnrichment)
        {
            return HistoryDotNetEnrichment.NotRequested;
        }

        if (string.IsNullOrWhiteSpace(policyPath))
        {
            return HistoryDotNetEnrichment.Unavailable("policy_required");
        }

        try
        {
            HistoryDotNetFactMaterialization facts = _factProvider.Materialize(
                request.RepositoryPath, result.ResolvedTo, policyPath);
            HistoryDotNetFileEnrichment[] files = result.LogicalFiles.Select(file => ProjectFile(file, facts)).ToArray();
            HistoryDotNetEnrichmentStatus status = files.Any(file => file.Status == HistoryDotNetFileEnrichmentStatus.Available)
                ? HistoryDotNetEnrichmentStatus.Available
                : HistoryDotNetEnrichmentStatus.NotApplicable;
            return new HistoryDotNetEnrichment(status, null, files);
        }
        catch (HistoryDotNetEnrichmentUnavailableException exception)
        {
            return HistoryDotNetEnrichment.Unavailable(exception.Reason);
        }
        catch (Exception)
        {
            return HistoryDotNetEnrichment.Unavailable("fact_materialization_failed");
        }
    }

    private static HistoryDotNetFileEnrichment ProjectFile(
        LogicalFile file,
        HistoryDotNetFactMaterialization facts)
    {
        if (!file.CanonicalPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || !facts.TypesByCanonicalPath.TryGetValue(file.CanonicalPath, out IReadOnlyList<HistoryDotNetTypeContext>? types)
            || types.Count == 0)
        {
            return new HistoryDotNetFileEnrichment(
                file.CanonicalPath, HistoryDotNetFileEnrichmentStatus.NotApplicable,
                Array.Empty<HistoryDotNetTypeContext>());
        }

        return new HistoryDotNetFileEnrichment(
            file.CanonicalPath, HistoryDotNetFileEnrichmentStatus.Available, types);
    }
}

internal sealed class HistoryDotNetEnrichmentUnavailableException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
