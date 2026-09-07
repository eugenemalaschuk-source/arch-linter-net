using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Performs only readiness and metric evaluation against the session supplied by the snapshot.
// Lifecycle checks, locking, cancellation terminal state, and setup materialization remain with
// ArchitectureAnalysisSnapshot.
internal static class ArchitectureAnalysisSnapshotMetricProjector
{
    internal static ArchitectureMetricMeasurementOutcome Measure(
        ArchitectureContractDocument document,
        IReadOnlyCollection<string>? metricIds,
        ArchitectureAnalysisSession session)
    {
        if (session.Context.MissingAssemblyNames.Count > 0)
        {
            return ArchitectureMetricEvaluator.Unavailable(
                document.Metrics,
                metricIds,
                document.Name,
                ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        return ArchitectureMetricEvaluator.Evaluate(session, document.Metrics, metricIds);
    }
}
