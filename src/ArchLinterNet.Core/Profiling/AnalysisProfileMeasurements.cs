using System.Diagnostics;

namespace ArchLinterNet.Core.Profiling;

// Environment-dependent process measurements. Both fields are null when no ValidationTiming
// instance backed the profiled run — see AnalysisProfileBuilder.
public sealed record AnalysisProfileMeasurements
{
    public long? PeakWorkingSetBytes { get; init; }

    public long? AllocatedBytesTotal { get; init; }

    // Resource metrics are collected independently from phase timing. A profile that opted into
    // timing must therefore expose this object even when a platform cannot supply a peak working
    // set value (macOS returns zero); null is reserved for an API caller that supplied no timing.
    public static AnalysisProfileMeasurements Capture(long allocatedBytesAtStart)
    {
        long peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
        return new AnalysisProfileMeasurements
        {
            PeakWorkingSetBytes = peakWorkingSetBytes > 0 ? peakWorkingSetBytes : null,
            AllocatedBytesTotal = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBytesAtStart),
        };
    }
}
