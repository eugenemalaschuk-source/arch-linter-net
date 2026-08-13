using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// The session's cache-facing view of finding-identity attribution: it owns the candidate log that
// accumulates as contracts execute and exposes the cursor that brackets one contract's candidates.
// The attribution algorithm itself lives in ArchitectureFindingIdentityAttributor (issue #452).
public sealed partial class ArchitectureAnalysisSession
{
    internal int FindingIdentityCursor => _findingIdentityCandidates.Count;

    internal IReadOnlyList<ArchitectureViolation> AttachFindingIdentities(
        IReadOnlyCollection<ArchitectureViolation> violations,
        int cursor)
    {
        return ArchitectureFindingIdentityAttributor.Attach(_findingIdentityCandidates, cursor, violations);
    }
}
