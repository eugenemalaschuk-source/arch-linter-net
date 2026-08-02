using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

// Testing API ownership wrapper for one ArchitectureAnalysisSnapshot (issue #363): a test
// explicitly creates this via ArchitectureValidationBuilder.CreateSnapshot(), evaluates strict
// and/or audit against the one shared snapshot, and disposes it deterministically (typically via
// a `using` block) instead of paying for one independent policy/project/assembly setup per
// assertion.
public sealed class ArchitectureValidationSnapshotSession(
    ArchitectureAnalysisSnapshot snapshot,
    ValidationTiming? timing,
    bool collectProfile = false,
    long allocatedBytesAtStart = 0)
    : IDisposable
{
    public ArchitectureValidationResult ValidateStrict()
    {
        return Evaluate("strict");
    }

    public ArchitectureValidationResult ValidateAudit()
    {
        return Evaluate("audit");
    }

    public ArchitectureAnalysisSnapshotCounters Counters => snapshot.Counters;

    private ArchitectureValidationResult Evaluate(string mode)
    {
        ValidationOutcome outcome = snapshot.Evaluate(mode, timing);

        AnalysisProfile? profile = collectProfile
            ? AnalysisProfileBuilder.Build(
                snapshot.Counters, timing, renderedSinkCount: 0, outputSinkCount: 0,
                ArchitectureValidationBuilder.ResolveCompletionStatus(outcome), cancellationObserved: false,
                ArchitectureValidationBuilder.CaptureMeasurements(allocatedBytesAtStart))
            : null;

        return ArchitectureValidationResultMapper.ToResult(outcome, timing, mode, profile);
    }

    public void Dispose()
    {
        snapshot.Dispose();
    }
}
