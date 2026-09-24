using System.Diagnostics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Enrichment;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History;

// This is the only bridge from the normal architecture policy lifecycle into history ingestion.
// Keeping it in Core prevents the CLI from reaching into Core.Contracts or inventing a parallel
// configuration reader, while the constructed ingestion service retains the ordinary fail-closed
// result/diagnostic boundary.
internal static class HistoryPolicyIngestionService
{
    public static HistoryIngestionOutcome Ingest(
        HistoryIngestionRequest request,
        string? policyPath,
        CancellationToken cancellationToken = default,
        HistoryIngestionTiming? timing = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskKeyExtraction taskExtraction;
        HistoryAnalysisConfiguration configuration;
        Stopwatch? policyClock = timing?.Start();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            configuration = string.IsNullOrWhiteSpace(policyPath)
                ? new HistoryAnalysisConfiguration()
                : new ArchitecturePolicyDocumentLoader().Load(policyPath, cancellationToken).HistoryAnalysis;
            taskExtraction = TaskKeyExtraction.FromConfiguration(configuration);
        }
        catch (InvalidOperationException exception)
        {
            timing?.Record("policy", policyClock);
            return HistoryIngestionOutcome.Failure(new HistoryDiagnostic(
                HistoryDiagnosticKind.ConfigurationInvalid,
                $"history_analysis policy configuration is invalid: {exception.Message}"));
        }

        timing?.Record("policy", policyClock);

        HistoryIngestionOutcome outcome = new HistoryIngestionService(taskExtraction, configuration).Ingest(
            request, cancellationToken, timing);
        if (outcome.Result is HistoryIngestionResult result)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch? enrichmentClock = timing?.Start();
            HistoryDotNetEnrichment dotNetEnrichment = new HistoryDotNetEnricher().Enrich(result, request, policyPath);
            cancellationToken.ThrowIfCancellationRequested();
            result.ApplyEnrichment(dotNetEnrichment.ToReportProjection(result.ResolvedTo));
            timing?.Record("enrichment", enrichmentClock);
        }

        return outcome;
    }
}
