namespace ArchLinterNet.Core.Validation;

// This projection is deliberately owned by the Validation application seam. It carries only the
// stable strings a review host needs, so Execution's observed subjects/dependencies and their
// scanner-backed generic collections never cross into Core.Topology.
internal sealed record ArchitectureTopologyObservation(
    IReadOnlyList<ArchitectureTopologyObservedSubject> Subjects,
    IReadOnlyList<ArchitectureTopologyObservedDependency> Dependencies);

internal sealed record ArchitectureTopologyObservedSubject(
    string Identity,
    string Subject,
    string Project,
    string Assembly);

internal sealed record ArchitectureTopologyObservedDependency(
    string SourceIdentity,
    string TargetIdentity,
    string Witness);
