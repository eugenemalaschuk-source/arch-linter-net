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

        (IReadOnlyList<ObservedSubject> subjects, IReadOnlyList<ObservedDependency> dependencies) =
            ObserveForValidation(session, topology.SubjectKind);
        return Evaluate(session, topology, subjects, dependencies);
    }

    // Existing validation reports, SARIF identities, and relationship results must not change
    // merely because a policy has a declared topology; the executor opts into this projection.
    private static (IReadOnlyList<ObservedSubject> Subjects, IReadOnlyList<ObservedDependency> Dependencies)
        ObserveForValidation(ArchitectureAnalysisSession session, string subjectKind)
    {
        Type[] types = session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        var subjectByType = new Dictionary<Type, ObservedSubject>();
        var subjectsByIdentity = new Dictionary<string, ObservedSubject>(StringComparer.Ordinal);

        foreach (Type type in types)
        {
            string assembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;
            string project = ResolveProject(session, assembly);
            string subject = subjectKind switch
            {
                "type" => ArchitectureTypeNames.SafeFullName(type),
                "namespace" => ArchitectureTypeNames.SafeNamespace(type),
                "project" => project,
                "assembly" => assembly,
                _ => throw new InvalidOperationException($"Unsupported topology subject kind '{subjectKind}'."),
            };
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

        return (
            subjectsByIdentity.Values.OrderBy(subject => subject.Identity, StringComparer.Ordinal).ToArray(),
            dependencies.OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
                .ToArray());
    }
}
