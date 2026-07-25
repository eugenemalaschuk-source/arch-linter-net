using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

// Testing API ownership wrapper for one ArchitectureAnalysisSnapshot (issue #363): a test
// explicitly creates this via ArchitectureValidationBuilder.CreateSnapshot(), evaluates strict
// and/or audit against the one shared snapshot, and disposes it deterministically (typically via
// a `using` block) instead of paying for one independent policy/project/assembly setup per
// assertion.
public sealed class ArchitectureValidationSnapshotSession(ArchitectureAnalysisSnapshot snapshot, ValidationTiming? timing)
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
        return ArchitectureValidationResultMapper.ToResult(outcome, timing);
    }

    public void Dispose()
    {
        snapshot.Dispose();
    }
}
