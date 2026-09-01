using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Internal immutable candidate receipt retained by one analysis snapshot for baseline comparison.
/// It deliberately carries values, not a runner or session, so the debt gate cannot trigger a
/// second policy composition, project-graph traversal, assembly load, or contract execution.
/// </summary>
internal sealed record ArchitectureSnapshotBaselineCandidateReceipt(
    ArchitectureContractDocument Document,
    IReadOnlyList<ArchitectureBaselineCandidate>? Candidates,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics);
