using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Session-owned projection of the direct external dependency facts used by measure-first
// metrics. It shares the external resolver and both declared-type and IL method-body detectors
// with validation, but projects facts directly instead of manufacturing violations.
internal sealed class ArchitectureExternalDependencyFactIndex
{
    private readonly ArchitectureAnalysisSession _session;
    private readonly Lazy<FactProjection> _projection;

    internal ArchitectureExternalDependencyFactIndex(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _projection = new Lazy<FactProjection>(
            Materialize,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal IReadOnlyList<ArchitectureExternalDependencyFact> Facts => _projection.Value.Facts;

    internal IReadOnlySet<Type> IncompleteSourceTypes => _projection.Value.IncompleteSourceTypes;

    private FactProjection Materialize()
    {
        Dictionary<string, ArchitectureExternalDependencyGroup> groups = _session.Document.ExternalDependencies;
        if (groups.Count == 0)
        {
            return FactProjection.Empty;
        }

        Type[] sourceTypes = _session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        var facts = new HashSet<ArchitectureExternalDependencyFact>();
        var incompleteSourceTypes = new HashSet<Type>();
        ArchitectureExternalDependencyIlScanner ilScanner = new();
        foreach ((string groupName, ArchitectureExternalDependencyGroup group) in groups
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (Type sourceType in sourceTypes)
            {
                _session.Context.CancellationToken.ThrowIfCancellationRequested();
                bool isComplete = _session.ReferenceGraph.TryGetReferencedTypes(
                    sourceType,
                    out IReadOnlyList<Type> referencedTypes);
                if (!isComplete)
                {
                    incompleteSourceTypes.Add(sourceType);
                }

                foreach (Type targetType in referencedTypes
                             .Distinct()
                             .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal))
                {
                    _session.Context.CancellationToken.ThrowIfCancellationRequested();
                    string fullName = ArchitectureTypeNames.SafeFullName(targetType);
                    if (string.IsNullOrEmpty(fullName))
                    {
                        continue;
                    }

                    string namespaceName = ArchitectureTypeNames.SafeNamespace(targetType);
                    if (ArchitectureExternalDependencyResolver.MatchesGroup(group, fullName, namespaceName))
                    {
                        facts.Add(new ArchitectureExternalDependencyFact(sourceType, fullName, groupName));
                    }
                }
            }

            ArchitectureExternalDependencyIlScanResult ilFacts = ilScanner.FindMethodBodyFactsWithCompleteness(
                sourceTypes, group, _session.Context.CancellationToken);
            incompleteSourceTypes.UnionWith(ilFacts.IncompleteSourceTypes);
            foreach (ArchitectureExternalDependencyIlFact fact in ilFacts.Facts)
            {
                facts.Add(new ArchitectureExternalDependencyFact(fact.SourceType, fact.TargetType, groupName));
            }
        }

        return new FactProjection(
            facts
                .OrderBy(fact => ArchitectureTypeNames.SafeFullName(fact.SourceType), StringComparer.Ordinal)
                .ThenBy(fact => fact.TargetIdentity, StringComparer.Ordinal)
                .ThenBy(fact => fact.GroupName, StringComparer.Ordinal)
                .ToArray(),
            incompleteSourceTypes);
    }

    private sealed record FactProjection(
        IReadOnlyList<ArchitectureExternalDependencyFact> Facts,
        IReadOnlySet<Type> IncompleteSourceTypes)
    {
        internal static FactProjection Empty { get; } = new(
            Array.Empty<ArchitectureExternalDependencyFact>(),
            new HashSet<Type>());
    }
}

internal sealed record ArchitectureExternalDependencyFact(
    Type SourceType,
    string TargetIdentity,
    string GroupName);
