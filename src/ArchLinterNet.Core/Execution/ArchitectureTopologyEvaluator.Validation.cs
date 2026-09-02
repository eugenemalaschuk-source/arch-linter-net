using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static partial class ArchitectureTopologyEvaluator
{
    // Normal validation deliberately retains its historical type-derived projection and finding
    // identity. Measure-first metrics use Evaluate, which needs canonical ownership and explicit
    // evidence completeness.
    internal static Result EvaluateForValidation(ArchitectureAnalysisSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArchitectureTopology? topology = session.Document.Topology;
        if (topology is null)
        {
            return Result.Empty;
        }

        ValidationObservation observation = ObserveForValidation(session, topology.SubjectKind);
        return Evaluate(session, topology, observation.Subjects, observation.Dependencies);
    }

    // This is the canonical normal-validation observation projection. Capture deliberately calls
    // this same seam instead of rebuilding a graph or using the richer metric projection. Keeping
    // the observation itself internal ensures scanner/session implementation details never become
    // part of the public capture API.
    internal static ValidationObservation ObserveForValidation(
        ArchitectureAnalysisSession session,
        string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(session);

        (IReadOnlyList<ObservedSubject> subjects, IReadOnlyList<ObservedDependency> dependencies) =
            ObserveForValidationCore(session, subjectKind);
        return new ValidationObservation(subjects, dependencies);
    }

    // Existing validation reports, SARIF identities, and relationship results must not change
    // merely because a policy has a declared topology; the executor opts into this projection.
    private static (IReadOnlyList<ObservedSubject> Subjects, IReadOnlyList<ObservedDependency> Dependencies)
        ObserveForValidationCore(ArchitectureAnalysisSession session, string subjectKind)
    {
        Type[] types = session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        (Dictionary<Type, ObservedSubject> subjectByType, IReadOnlyList<ObservedSubject> subjects) =
            ObserveSubjects(session, subjectKind, types);
        IReadOnlyList<ObservedDependency> dependencies = ObserveDependencies(session, types, subjectByType);
        return (subjects, dependencies);
    }

    private static (Dictionary<Type, ObservedSubject> ByType, IReadOnlyList<ObservedSubject> Subjects) ObserveSubjects(
        ArchitectureAnalysisSession session,
        string subjectKind,
        IEnumerable<Type> types)
    {
        var subjectByType = new Dictionary<Type, ObservedSubject>();
        var subjectsByIdentity = new Dictionary<string, ObservedSubject>(StringComparer.Ordinal);

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
            if (!subjectsByIdentity.TryGetValue(identity, out ObservedSubject? observed))
            {
                observed = new ObservedSubject(identity, project, assembly, subject, type);
                subjectsByIdentity.Add(identity, observed);
            }

            subjectByType[type] = observed;
        }

        return (
            subjectByType,
            subjectsByIdentity.Values.OrderBy(subject => subject.Identity, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<ObservedDependency> ObserveDependencies(
        ArchitectureAnalysisSession session,
        IEnumerable<Type> types,
        IReadOnlyDictionary<Type, ObservedSubject> subjectByType)
    {
        var dependencies = new HashSet<ObservedDependency>();
        foreach (Type source in types)
        {
            if (!subjectByType.TryGetValue(source, out ObservedSubject? sourceSubject))
            {
                continue;
            }

            foreach (Type target in session.ReferenceGraph.GetReferencedTypes(source))
            {
                if (!subjectByType.TryGetValue(target, out ObservedSubject? targetSubject)
                    || string.Equals(sourceSubject.Identity, targetSubject.Identity, StringComparison.Ordinal))
                {
                    continue;
                }

                string witness = $"{ArchitectureTypeNames.SafeFullName(source)} -> {ArchitectureTypeNames.SafeFullName(target)}";
                dependencies.Add(new ObservedDependency(sourceSubject.Identity, targetSubject.Identity, witness));
            }
        }

        return dependencies.OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveSubject(string subjectKind, Type type, string project, string assembly)
    {
        return subjectKind switch
        {
            "type" => ArchitectureTypeNames.SafeFullName(type),
            "namespace" => ArchitectureTypeNames.SafeNamespace(type),
            "project" => project,
            "assembly" => assembly,
            _ => throw new InvalidOperationException($"Unsupported topology subject kind '{subjectKind}'."),
        };
    }

    internal sealed record ValidationObservation(
        IReadOnlyList<ObservedSubject> Subjects,
        IReadOnlyList<ObservedDependency> Dependencies);
}
