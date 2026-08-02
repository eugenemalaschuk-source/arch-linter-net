using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureValidationApplicationService
{
    ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null);

    // Composes policy, evaluates the project graph, loads assemblies, and runs build-state
    // preflight once, returning a caller-owned ArchitectureAnalysisSnapshot that any number of
    // strict/audit Evaluate calls can be served from. The caller is responsible for disposing the
    // returned snapshot (see ArchitectureAnalysisSnapshot.Dispose).
    ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing = null);

    // Same behavior as Validate, plus the snapshot's typed counters (policy compositions, project
    // graph evaluations, assembly loads) — for issue #374's analysis-profile/v1, which needs these
    // for a single-mode run without changing Validate's existing signature or behavior. See
    // openspec/specs/analysis-profile/spec.md, "Counters prove the one-snapshot and
    // sink-count-only invariants".
    (ValidationOutcome Outcome, ArchitectureAnalysisSnapshotCounters Counters) ValidateWithCounters(
        ValidationRequest request, ValidationTiming? timing = null);
}
