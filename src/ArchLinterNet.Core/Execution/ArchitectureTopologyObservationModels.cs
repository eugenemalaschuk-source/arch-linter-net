using System.Reflection;

namespace ArchLinterNet.Core.Execution;

// Internal topology observation DTOs keep scanner/session facts at the Execution boundary while
// allowing the policy evaluator to consume a stable, purpose-named projection.
internal sealed record ArchitectureTopologyObservedSubject(
    string Identity,
    string Project,
    string Assembly,
    string Subject,
    Type? Type = null,
    string? CanonicalAssemblyIdentity = null,
    string? AssemblyReferenceIdentity = null,
    Assembly? ResolvedAssembly = null,
    string? ProjectSelectorIdentity = null);

internal sealed record ArchitectureTopologyObservedDependency(
    string SourceIdentity,
    string TargetIdentity,
    string Witness,
    ArchitectureTopologyAssemblyEndpointBinding SourceBinding = ArchitectureTopologyAssemblyEndpointBinding.Bound,
    ArchitectureTopologyAssemblyEndpointBinding TargetBinding = ArchitectureTopologyAssemblyEndpointBinding.Bound,
    string? SourceAssemblyName = null,
    string? TargetAssemblyName = null);

internal sealed record ArchitectureTopologyObservation(
    IReadOnlyList<ArchitectureTopologyObservedSubject> Subjects,
    IReadOnlyList<ArchitectureTopologyObservedDependency> Dependencies,
    IReadOnlySet<string> IncompleteDependencySourceIdentities);

internal sealed record ArchitectureTopologyAssemblyDependencyObservation(
    string SourceAssemblyName,
    string SourceCanonicalAssemblyIdentity,
    IReadOnlyList<ArchitectureTopologyAssemblyReferenceObservation> References);

internal sealed record ArchitectureTopologyAssemblyReferenceObservation(
    string AssemblyName,
    string ReferenceIdentity);

internal enum ArchitectureTopologyAssemblyEndpointBinding
{
    Bound,
    Missing,
    Ambiguous,
}
