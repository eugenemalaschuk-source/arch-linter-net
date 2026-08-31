using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureMetricMeasurementApplicationService
{
    ArchitectureMetricMeasurementOutcome Measure(
        ArchitectureMetricMeasurementRequest request,
        ValidationTiming? timing = null);
}
