using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    /// <summary>Measures selected policy metrics from this snapshot without creating findings.</summary>
    public ArchitectureMetricMeasurementOutcome Measure(IReadOnlyCollection<string>? metricIds = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cancelled)
            {
                throw new OperationCanceledException(
                    "This snapshot observed cancellation during a prior operation and cannot be reused.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            if (_preflight.Blocked)
            {
                return ArchitectureMetricEvaluator.Unavailable(
                    _document.Metrics, metricIds, _document.Name,
                    ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            }

            ArchitectureRunnerSetup setup = EnsureSetup();
            return ArchitectureMetricEvaluator.Evaluate(setup.Runner.Session, _document.Metrics, metricIds);
        }
    }
}
