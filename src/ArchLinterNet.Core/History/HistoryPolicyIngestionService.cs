using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Enrichment;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History;

// This is the only bridge from the normal architecture policy lifecycle into history ingestion.
// Keeping it in Core prevents the CLI from reaching into Core.Contracts or inventing a parallel
// configuration reader, while the constructed ingestion service retains the ordinary fail-closed
// result/diagnostic boundary.
internal sealed class HistoryPolicyIngestionService
{
    public static HistoryPolicyIngestionService Default { get; } = new();

    public HistoryIngestionOutcome Ingest(HistoryIngestionRequest request, string? policyPath)
    {
        TaskKeyExtraction taskExtraction;
        HistoryAnalysisConfiguration configuration;
        try
        {
            configuration = string.IsNullOrWhiteSpace(policyPath)
                ? new HistoryAnalysisConfiguration()
                : new ArchitecturePolicyDocumentLoader().Load(policyPath).HistoryAnalysis;
            taskExtraction = TaskKeyExtraction.FromConfiguration(configuration);
        }
        catch (InvalidOperationException exception)
        {
            return HistoryIngestionOutcome.Failure(new HistoryDiagnostic(
                HistoryDiagnosticKind.ConfigurationInvalid,
                $"history_analysis policy configuration is invalid: {exception.Message}"));
        }

        HistoryIngestionOutcome outcome = new HistoryIngestionService(taskExtraction, configuration).Ingest(request);
        if (outcome.Result is HistoryIngestionResult result)
        {
            HistoryDotNetEnrichment dotNetEnrichment = new HistoryDotNetEnricher().Enrich(result, request, policyPath);
            result.ApplyEnrichment(dotNetEnrichment.ToReportProjection(result.ResolvedTo));
        }

        return outcome;
    }
}
