using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// The session's cache-facing view of finding-identity attribution: it owns the candidate log that
// accumulates as contracts execute and exposes the cursor that brackets one contract's candidates.
// The attribution algorithm itself lives in ArchitectureFindingIdentityAttributor (issue #452).
internal sealed class ArchitectureFindingIdentityService
{
    private readonly List<ArchitectureBaselineCandidate> _candidates = new();

    public List<ArchitectureBaselineCandidate> Candidates => _candidates;

    public int Cursor => _candidates.Count;

    public IReadOnlyList<ArchitectureViolation> Attach(
        IReadOnlyCollection<ArchitectureViolation> violations,
        int cursor)
    {
        return ArchitectureFindingIdentityAttributor.Attach(_candidates, cursor, violations);
    }
}
