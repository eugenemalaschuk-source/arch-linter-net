using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

/// <summary>Composes one analysis snapshot and evaluates only the requested read-only metrics.</summary>
public sealed class ArchitectureMetricMeasurementApplicationService(
    IArchitectureValidationApplicationService validationApplicationService)
    : IArchitectureMetricMeasurementApplicationService
{
    public ArchitectureMetricMeasurementOutcome Measure(
        ArchitectureMetricMeasurementRequest request,
        ValidationTiming? timing = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        using ArchitectureAnalysisSnapshot snapshot = validationApplicationService.CreateSnapshot(
            request.ToSnapshotRequest(), timing);
        return snapshot.Measure(request.MetricIds);
    }
}
