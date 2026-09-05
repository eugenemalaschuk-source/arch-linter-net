using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Owns the historical normal-validation topology observation. Capture uses this same observer
// so its compatibility identities and type-reference witnesses remain structurally shared with
// ordinary validation.
internal static class ArchitectureTopologyValidationObserver
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

    internal static ArchitectureTopologyObservation Observe(
        ArchitectureAnalysisSession session,
        string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(session);

        Type[] types = session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        (Dictionary<Type, ArchitectureTopologyObservedSubject> subjectByType,
            IReadOnlyList<ArchitectureTopologyObservedSubject> subjects) =
            ObserveSubjects(session, subjectKind, types);
        IReadOnlyList<ArchitectureTopologyObservedDependency> dependencies =
            ObserveDependencies(session, types, subjectByType);
        return new ArchitectureTopologyObservation(
            subjects,
            dependencies,
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static (
        Dictionary<Type, ArchitectureTopologyObservedSubject> ByType,
        IReadOnlyList<ArchitectureTopologyObservedSubject> Subjects) ObserveSubjects(
        ArchitectureAnalysisSession session,
        string subjectKind,
        IEnumerable<Type> types)
    {
        var subjectByType = new Dictionary<Type, ArchitectureTopologyObservedSubject>();
        var subjectsByIdentity = new Dictionary<string, ArchitectureTopologyObservedSubject>(StringComparer.Ordinal);

        foreach (Type type in types)
        {
            string assembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;
            string project = ResolveProject(session, assembly);
            string subject = ResolveSubject(subjectKind, type, project, assembly);
            if (string.IsNullOrEmpty(subject))
            {
                continue;
            }

            string identity = BuildValidationIdentity(subjectKind, project, assembly, subject);
            if (!subjectsByIdentity.TryGetValue(identity, out ArchitectureTopologyObservedSubject? observed))
            {
                observed = new ArchitectureTopologyObservedSubject(identity, project, assembly, subject, type);
                subjectsByIdentity.Add(identity, observed);
            }

            subjectByType[type] = observed;
        }

        return (
            subjectByType,
            subjectsByIdentity.Values.OrderBy(subject => subject.Identity, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<ArchitectureTopologyObservedDependency> ObserveDependencies(
        ArchitectureAnalysisSession session,
        IEnumerable<Type> types,
        IReadOnlyDictionary<Type, ArchitectureTopologyObservedSubject> subjectByType)
    {
        var dependencies = new HashSet<ArchitectureTopologyObservedDependency>();
        foreach (Type source in types)
        {
            if (!subjectByType.TryGetValue(source, out ArchitectureTopologyObservedSubject? sourceSubject))
            {
                continue;
            }

            foreach (Type target in session.ReferenceGraph.GetReferencedTypes(source))
            {
                if (!subjectByType.TryGetValue(target, out ArchitectureTopologyObservedSubject? targetSubject)
                    || string.Equals(sourceSubject.Identity, targetSubject.Identity, StringComparison.Ordinal))
                {
                    continue;
                }

                string witness = string.Concat(
                    ArchitectureTypeNames.SafeFullName(source),
                    " -> ",
                    ArchitectureTypeNames.SafeFullName(target));
                dependencies.Add(new ArchitectureTopologyObservedDependency(
                    sourceSubject.Identity, targetSubject.Identity, witness));
            }
        }

        return dependencies.OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveSubject(string subjectKind, Type type, string project, string assembly) =>
        subjectKind switch
        {
            "type" => ArchitectureTypeNames.SafeFullName(type),
            "namespace" => ArchitectureTypeNames.SafeNamespace(type),
            "project" => project,
            "assembly" => assembly,
            _ => throw new InvalidOperationException(
                string.Concat(
                    "Unsupported topology subject kind '",
                    subjectKind,
                    "'",
                    ".")),
        };

    private static string ResolveProject(ArchitectureAnalysisSession session, string assembly)
    {
        if (session.Facts.TryGetProjectByAssemblyName(assembly, out var project))
        {
            return project.AssemblyName;
        }

        // A prepared assembly may not have a project-discovery record (for example a fixture
        // assembly). Its simple assembly identity is the historical fallback for validation.
        return assembly;
    }

    private static string BuildValidationIdentity(string subjectKind, string project, string assembly, string subject) =>
        string.Concat(
            subjectKind,
            "|project=",
            project,
            "|assembly=",
            assembly,
            "|subject=",
            subject);
}
