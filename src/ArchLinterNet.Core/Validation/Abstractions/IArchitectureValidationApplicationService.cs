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
}
