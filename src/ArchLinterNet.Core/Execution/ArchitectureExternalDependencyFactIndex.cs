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
    private readonly Lazy<IReadOnlyList<ArchitectureExternalDependencyFact>> _facts;

    internal ArchitectureExternalDependencyFactIndex(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _facts = new Lazy<IReadOnlyList<ArchitectureExternalDependencyFact>>(
            Materialize,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal IReadOnlyList<ArchitectureExternalDependencyFact> Facts => _facts.Value;

    private IReadOnlyList<ArchitectureExternalDependencyFact> Materialize()
    {
        Dictionary<string, ArchitectureExternalDependencyGroup> groups = _session.Document.ExternalDependencies;
        if (groups.Count == 0)
        {
            return Array.Empty<ArchitectureExternalDependencyFact>();
        }

        Type[] sourceTypes = _session.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();
        var facts = new HashSet<ArchitectureExternalDependencyFact>();
        ArchitectureExternalDependencyIlScanner ilScanner = new();
        foreach ((string groupName, ArchitectureExternalDependencyGroup group) in groups
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (Type sourceType in sourceTypes)
            {
                _session.Context.CancellationToken.ThrowIfCancellationRequested();
                foreach (Type targetType in _session.ReferenceGraph.GetReferencedTypes(sourceType)
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

            foreach (ArchitectureExternalDependencyIlFact fact in ilScanner.FindMethodBodyFacts(
                         sourceTypes, group, _session.Context.CancellationToken))
            {
                facts.Add(new ArchitectureExternalDependencyFact(fact.SourceType, fact.TargetType, groupName));
            }
        }

        return facts
            .OrderBy(fact => ArchitectureTypeNames.SafeFullName(fact.SourceType), StringComparer.Ordinal)
            .ThenBy(fact => fact.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(fact => fact.GroupName, StringComparer.Ordinal)
            .ToArray();
    }
}

internal sealed record ArchitectureExternalDependencyFact(
    Type SourceType,
    string TargetIdentity,
    string GroupName);
