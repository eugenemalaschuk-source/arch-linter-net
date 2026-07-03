using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution.Abstractions;

public interface IArchitectureContractRunner
{
    ArchitectureAnalysisSession Session { get; }

    IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }

    IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; }

    List<ArchitectureViolation> CheckConfiguration();

    List<ArchitectureViolation> CheckConfiguration(bool strict);

    List<PolicyConsistencyDiagnostic> CheckPolicyConsistency();
}
