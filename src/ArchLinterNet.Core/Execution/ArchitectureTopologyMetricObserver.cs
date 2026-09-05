using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Owns the canonical resolved-artifact topology observation used by measure-first metrics. This
// projection intentionally differs from normal validation/capture while consuming the same
// session-owned type index, reference graph, project facts, and assembly metadata.
internal static class ArchitectureTopologyMetricObserver
{
    internal static ArchitectureTopologyEvaluator.Result Evaluate(ArchitectureAnalysisSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArchitectureTopology? topology = session.Document.Topology;
        if (topology is null)
        {
            return ArchitectureTopologyEvaluator.Result.Empty;
        }

        ArchitectureTopologyObservation observation = Observe(session, topology.SubjectKind);
        return ArchitectureTopologyEvaluator.Evaluate(
            session,
            topology,
            observation.Subjects,
            observation.Dependencies,
            observation.IncompleteDependencySourceIdentities);
    }

    // Kept internal for focused metric tests that need the exact projection consumed by metric
    // evaluation without reimplementing resolved-artifact ownership and identity selection.
    internal static ArchitectureTopologyEvaluator.Projection Project(
        ArchitectureAnalysisSession session,
        ArchitectureTopology topology)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(topology);

        ArchitectureTopologyObservation observation = Observe(session, topology.SubjectKind);
        return ArchitectureTopologyEvaluator.Project(
            session,
            topology,
            observation.Subjects,
            observation.Dependencies,
            observation.IncompleteDependencySourceIdentities);
    }

    internal static ArchitectureTopologyObservation Observe(
        ArchitectureAnalysisSession session,
        string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Assembly topology is an assembly-metadata projection. In particular, a resolved target
        // assembly can legitimately expose no loadable types, and must still remain a canonical
        // topology subject for its assembly-level dependency facts.
        if (subjectKind == "assembly")
        {
            (IReadOnlyList<ArchitectureTopologyObservedSubject> subjects,
                IReadOnlyList<ArchitectureTopologyObservedDependency> assemblyDependencies) = ObserveAssemblies(session);
            return new ArchitectureTopologyObservation(
                subjects,
                assemblyDependencies,
                new HashSet<string>(StringComparer.Ordinal));
        }

        Type[] types = session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        var subjectByType = new Dictionary<Type, ArchitectureTopologyObservedSubject>();
        var subjectsByIdentity = new Dictionary<string, ArchitectureTopologyObservedSubject>(StringComparer.Ordinal);

        foreach (Type type in types)
        {
            string assembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;
            string canonicalAssemblyIdentity = ResolveCanonicalAssemblyIdentity(type.Assembly);
            string assemblyReferenceIdentity = ResolveAssemblyReferenceIdentity(type.Assembly);
            string project = ResolveProjectForMetric(session, type);
            string projectSelectorIdentity = ResolveProjectSelectorForMetric(session, type);
            string subject = subjectKind switch
            {
                "type" => ArchitectureTypeNames.SafeFullName(type),
                "namespace" => ArchitectureTypeNames.SafeNamespace(type),
                "project" => project,
                _ => throw new InvalidOperationException($"Unsupported topology subject kind '{subjectKind}'."),
            };
            if (string.IsNullOrEmpty(subject))
            {
                continue;
            }

            string identity = BuildMetricSubjectIdentity(
                subjectKind,
                project,
                assembly,
                canonicalAssemblyIdentity,
                subject);
            if (!subjectsByIdentity.TryGetValue(identity, out ArchitectureTopologyObservedSubject? observed))
            {
                observed = new ArchitectureTopologyObservedSubject(
                    identity,
                    project,
                    assembly,
                    subject,
                    type,
                    canonicalAssemblyIdentity,
                    assemblyReferenceIdentity,
                    type.Assembly,
                    projectSelectorIdentity);
                subjectsByIdentity.Add(identity, observed);
            }

            subjectByType[type] = observed;
        }

        var dependencies = new HashSet<ArchitectureTopologyObservedDependency>();
        var incompleteDependencySourceIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type source in types)
        {
            if (!subjectByType.TryGetValue(source, out ArchitectureTopologyObservedSubject? sourceSubject))
            {
                continue;
            }

            bool isComplete = session.ReferenceGraph.TryGetReferencedTypes(source, out IReadOnlyList<Type> referencedTypes);
            if (!isComplete)
            {
                incompleteDependencySourceIdentities.Add(sourceSubject.Identity);
            }

            foreach (Type target in referencedTypes)
            {
                if (!subjectByType.TryGetValue(target, out ArchitectureTopologyObservedSubject? targetSubject)
                    || string.Equals(sourceSubject.Identity, targetSubject.Identity, StringComparison.Ordinal))
                {
                    continue;
                }

                string witness = $"{ArchitectureTypeNames.SafeFullName(source)} -> {ArchitectureTypeNames.SafeFullName(target)}";
                dependencies.Add(new ArchitectureTopologyObservedDependency(
                    sourceSubject.Identity,
                    targetSubject.Identity,
                    witness));
            }
        }

        return new ArchitectureTopologyObservation(
            subjectsByIdentity.Values.OrderBy(subject => subject.Identity, StringComparer.Ordinal).ToArray(),
            dependencies.OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
                .ToArray(),
            incompleteDependencySourceIdentities);
    }

    // Shared with metrics so external facts use the exact owner binding used by the metric
    // topology projection for a source type.
    internal static string ResolveProjectForMetric(ArchitectureAnalysisSession session, Type type) =>
        ResolveProjectForMetric(session, type.Assembly);

    internal static string ResolveCanonicalAssemblyIdentity(Type type) =>
        ResolveCanonicalAssemblyIdentity(type.Assembly);

    internal static string ResolveCanonicalAssemblyIdentity(Assembly assembly) =>
        CanonicalAssemblyIdentity(assembly);

    internal static string BuildMetricSubjectIdentity(
        string subjectKind,
        string project,
        string assembly,
        string canonicalAssemblyIdentity,
        string subject) =>
        $"{subjectKind}|project={project}|assembly={assembly}|canonical_assembly={canonicalAssemblyIdentity}|subject={subject}";

    // Kept internal for regression tests that model multiple resolved assemblies with one simple
    // name. Production observation supplies these records from real assembly metadata.
    internal static IReadOnlyList<ArchitectureTopologyObservedDependency> BindAssemblyDependencies(
        IReadOnlyList<ArchitectureTopologyObservedSubject> subjects,
        IReadOnlyList<ArchitectureTopologyAssemblyDependencyObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentNullException.ThrowIfNull(observations);

        Dictionary<string, ArchitectureTopologyObservedSubject[]> subjectsByAssembly = subjects
            .GroupBy(subject => subject.Assembly, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(subject => subject.Identity, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var dependencies = new HashSet<ArchitectureTopologyObservedDependency>();
        foreach (ArchitectureTopologyAssemblyDependencyObservation observation in observations
                     .OrderBy(item => item.SourceAssemblyName, StringComparer.Ordinal)
                     .ThenBy(item => item.SourceCanonicalAssemblyIdentity, StringComparer.Ordinal))
        {
            ArchitectureTopologyAssemblyEndpointBinding sourceBinding = BindAssemblyEndpoint(
                subjectsByAssembly,
                observation.SourceAssemblyName,
                observation.SourceCanonicalAssemblyIdentity,
                referenceIdentity: null,
                out string sourceIdentity);
            foreach (ArchitectureTopologyAssemblyReferenceObservation reference in observation.References
                         .OrderBy(item => item.AssemblyName, StringComparer.Ordinal)
                         .ThenBy(item => item.ReferenceIdentity, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(reference.AssemblyName))
                {
                    continue;
                }

                // The retained first-party graph includes every resolved assembly subject. A
                // metadata reference without a retained simple-name candidate is external, not an
                // unmapped topology endpoint.
                if (!subjectsByAssembly.ContainsKey(reference.AssemblyName))
                {
                    continue;
                }

                ArchitectureTopologyAssemblyEndpointBinding targetBinding = BindAssemblyEndpoint(
                    subjectsByAssembly,
                    reference.AssemblyName,
                    canonicalAssemblyIdentity: null,
                    reference.ReferenceIdentity,
                    out string targetIdentity);
                if (sourceBinding == ArchitectureTopologyAssemblyEndpointBinding.Bound
                    && targetBinding == ArchitectureTopologyAssemblyEndpointBinding.Bound
                    && string.Equals(sourceIdentity, targetIdentity, StringComparison.Ordinal))
                {
                    continue;
                }

                dependencies.Add(new ArchitectureTopologyObservedDependency(
                    sourceIdentity,
                    targetIdentity,
                    $"{observation.SourceAssemblyName} -> {reference.AssemblyName}",
                    sourceBinding,
                    targetBinding,
                    observation.SourceAssemblyName,
                    reference.AssemblyName));
            }
        }

        return dependencies.OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
            .ToArray();
    }

    private static (IReadOnlyList<ArchitectureTopologyObservedSubject> Subjects,
        IReadOnlyList<ArchitectureTopologyObservedDependency> Dependencies) ObserveAssemblies(
        ArchitectureAnalysisSession session)
    {
        Assembly[] assemblies = session.Context.TargetAssemblies
            .OrderBy(candidate => candidate.GetName().Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(CanonicalAssemblyIdentity, StringComparer.Ordinal)
            .ToArray();
        var subjectsByIdentity = new Dictionary<string, ArchitectureTopologyObservedSubject>(StringComparer.Ordinal);
        foreach (Assembly assembly in assemblies)
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            if (string.IsNullOrEmpty(assemblyName))
            {
                continue;
            }

            string canonicalAssemblyIdentity = CanonicalAssemblyIdentity(assembly);
            string project = ResolveProjectForMetric(session, assembly);
            string projectSelectorIdentity = ResolveProjectSelectorForMetric(session, assembly);
            string identity = BuildMetricSubjectIdentity(
                "assembly",
                project,
                assemblyName,
                canonicalAssemblyIdentity,
                assemblyName);
            subjectsByIdentity.TryAdd(
                identity,
                new ArchitectureTopologyObservedSubject(
                    identity,
                    project,
                    assemblyName,
                    assemblyName,
                    CanonicalAssemblyIdentity: canonicalAssemblyIdentity,
                    AssemblyReferenceIdentity: ResolveAssemblyReferenceIdentity(assembly),
                    ResolvedAssembly: assembly,
                    ProjectSelectorIdentity: projectSelectorIdentity));
        }

        ArchitectureTopologyAssemblyDependencyObservation[] observations = assemblies
            .Where(assembly => !string.IsNullOrEmpty(assembly.GetName().Name))
            .Select(ToAssemblyDependencyObservation)
            .ToArray();
        ArchitectureTopologyObservedSubject[] subjects = subjectsByIdentity.Values
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .ToArray();
        return (subjects, BindAssemblyDependencies(subjects, observations));
    }

    private static ArchitectureTopologyAssemblyDependencyObservation ToAssemblyDependencyObservation(Assembly assembly) => new(
        assembly.GetName().Name!,
        CanonicalAssemblyIdentity(assembly),
        assembly.GetReferencedAssemblies()
            .OrderBy(reference => reference.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(ResolveAssemblyReferenceIdentity, StringComparer.Ordinal)
            .Select(reference => new ArchitectureTopologyAssemblyReferenceObservation(
                reference.Name ?? string.Empty,
                ResolveAssemblyReferenceIdentity(reference)))
            .ToArray());

    private static ArchitectureTopologyAssemblyEndpointBinding BindAssemblyEndpoint(
        IReadOnlyDictionary<string, ArchitectureTopologyObservedSubject[]> subjectsByAssembly,
        string assemblyName,
        string? canonicalAssemblyIdentity,
        string? referenceIdentity,
        out string identity)
    {
        if (!subjectsByAssembly.TryGetValue(assemblyName, out ArchitectureTopologyObservedSubject[]? candidates))
        {
            identity = UnboundAssemblyEndpointIdentity(assemblyName, referenceIdentity ?? canonicalAssemblyIdentity);
            return ArchitectureTopologyAssemblyEndpointBinding.Missing;
        }

        if (candidates.Length != 1)
        {
            identity = UnboundAssemblyEndpointIdentity(assemblyName, referenceIdentity ?? canonicalAssemblyIdentity);
            return ArchitectureTopologyAssemblyEndpointBinding.Ambiguous;
        }

        ArchitectureTopologyObservedSubject candidate = candidates[0];
        bool canonicalMatches = string.IsNullOrEmpty(canonicalAssemblyIdentity)
            || string.Equals(candidate.CanonicalAssemblyIdentity, canonicalAssemblyIdentity, StringComparison.Ordinal);
        bool referenceMatches = string.IsNullOrEmpty(referenceIdentity)
            || string.Equals(candidate.AssemblyReferenceIdentity, referenceIdentity, StringComparison.Ordinal);
        if (canonicalMatches && referenceMatches)
        {
            identity = candidate.Identity;
            return ArchitectureTopologyAssemblyEndpointBinding.Bound;
        }

        identity = UnboundAssemblyEndpointIdentity(assemblyName, referenceIdentity ?? canonicalAssemblyIdentity);
        return ArchitectureTopologyAssemblyEndpointBinding.Ambiguous;
    }

    private static string ResolveProjectForMetric(ArchitectureAnalysisSession session, Assembly assembly) =>
        session.Facts.TryGetProjectByResolvedAssembly(assembly, out ArchitectureDiscoveredProject? project)
            ? ProjectPathNormalizer.Normalize(project.Path)
            : $"unbound-project|{CanonicalAssemblyIdentity(assembly)}";

    private static string ResolveProjectSelectorForMetric(ArchitectureAnalysisSession session, Type type) =>
        ResolveProjectSelectorForMetric(session, type.Assembly);

    private static string ResolveProjectSelectorForMetric(ArchitectureAnalysisSession session, Assembly assembly) =>
        session.Facts.TryGetProjectByResolvedAssembly(assembly, out ArchitectureDiscoveredProject? project)
            ? project.AssemblyName
            : assembly.GetName().Name ?? string.Empty;

    private static string UnboundAssemblyEndpointIdentity(string assemblyName, string? identity) =>
        $"assembly-endpoint|assembly={assemblyName}|identity={identity ?? string.Empty}";

    private static string CanonicalAssemblyIdentity(Assembly assembly)
    {
        try
        {
            return $"{ResolveAssemblyReferenceIdentity(assembly)}|mvid={assembly.ManifestModule.ModuleVersionId:N}";
        }
        catch (NotSupportedException)
        {
            return ResolveAssemblyReferenceIdentity(assembly);
        }
    }

    private static string ResolveAssemblyReferenceIdentity(Assembly assembly) => assembly.FullName
        ?? assembly.GetName().FullName
        ?? assembly.GetName().Name
        ?? string.Empty;

    private static string ResolveAssemblyReferenceIdentity(AssemblyName assemblyName) => assemblyName.FullName
        ?? assemblyName.Name
        ?? string.Empty;
}
