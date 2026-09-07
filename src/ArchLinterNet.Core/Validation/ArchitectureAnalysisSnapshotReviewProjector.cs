using ArchLinterNet.Core.Execution;

namespace ArchLinterNet.Core.Validation;

// Converts Execution-owned topology observations to the neutral review DTO consumed by the
// Validation/Topology application seam. It has no session or lifecycle ownership of its own.
internal static class ArchitectureAnalysisSnapshotReviewProjector
{
    internal static ArchitectureTopologyObservation Capture(
        ArchitectureAnalysisSession session,
        string subjectKind)
    {
        global::ArchLinterNet.Core.Execution.ArchitectureTopologyObservation observation =
            ArchitectureTopologyValidationObserver.Observe(session, subjectKind);
        return new ArchitectureTopologyObservation(
            observation.Subjects.Select(subject => new ArchitectureTopologyObservedSubject(
                subject.Identity, subject.Subject, subject.Project, subject.Assembly)).ToArray(),
            observation.Dependencies.Select(dependency => new ArchitectureTopologyObservedDependency(
                dependency.SourceIdentity, dependency.TargetIdentity, dependency.Witness)).ToArray());
    }
}
