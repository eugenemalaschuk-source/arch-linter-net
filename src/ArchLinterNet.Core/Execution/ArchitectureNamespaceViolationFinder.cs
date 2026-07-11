using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureNamespaceViolationFinder
{
    public static IEnumerable<ArchitectureViolation> FindNamespaceViolations(
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureReferenceGraph? referenceGraph = null,
        ArchitectureRoleIndex? roleIndex = null)
    {
        return sourceTypes
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                var forbiddenMatches = (referenceGraph != null
                        ? referenceGraph.GetReferencedTypes(type)
                        : ArchitectureReferenceScanner.GetReferencedTypes(type))
                    .Select(reference => new
                    {
                        Reference = reference,
                        Match = MatchReference(forbiddenLayer, reference, roleIndex)
                    })
                    .Where(x => x.Match.Matched)
                    .Select(x => new
                    {
                        FullName = ArchitectureTypeNames.SafeFullName(x.Reference),
                        x.Match.MatchedNamespacePrefix
                    })
                    .Where(x => !string.IsNullOrEmpty(x.FullName))
                    .Where(x => !allowedTypeFullNames.Contains(x.FullName))
                    .Where(x => !executionContext.IsIgnored(sourceFullName, x.FullName))
                    .GroupBy(x => x.FullName, StringComparer.Ordinal)
                    .Select(group => new
                    {
                        FullName = group.Key,
                        MatchedNamespacePrefix = group.Select(x => x.MatchedNamespacePrefix)
                            .FirstOrDefault(prefix => !string.IsNullOrEmpty(prefix))
                    })
                    .OrderBy(x => x.FullName, StringComparer.Ordinal)
                    .ToArray();

                string[] forbiddenRefs = forbiddenMatches.Select(x => x.FullName).ToArray();
                string[] matchedPrefixes = forbiddenMatches
                    .Select(x => x.MatchedNamespacePrefix)
                    .Where(prefix => !string.IsNullOrEmpty(prefix))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(prefix => prefix, StringComparer.Ordinal)
                    .ToArray()!;

                return new ArchitectureViolation(
                    executionContext.ContractName, executionContext.ContractId, sourceFullName,
                    ArchitectureLayerResolver.DescribeLayer(forbiddenLayer), forbiddenRefs)
                {
                    MatchedNamespacePrefixes = matchedPrefixes.Length > 0 ? matchedPrefixes : null
                };
            })
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToArray();
    }

    public static IEnumerable<ArchitectureViolation> FindTransitiveNamespaceViolations(
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        IReadOnlyCollection<Assembly> targetAssemblies,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureReferenceGraph? referenceGraph = null,
        ArchitectureRoleIndex? roleIndex = null)
    {
        HashSet<Assembly> assemblySet = targetAssemblies.ToHashSet();
        Func<Type, bool> traversePredicate = t => assemblySet.Contains(t.Assembly);

        return sourceTypes
            .OrderBy(type => ArchitectureTypeNames.SafeFullName(type), StringComparer.Ordinal)
            .Select(type => BuildTransitiveViolation(
                type, forbiddenLayer, allowedTypeFullNames, executionContext, referenceGraph, traversePredicate, roleIndex))
            .Where(violation => violation != null)!;
    }

    private static ArchitectureViolation? BuildTransitiveViolation(
        Type type,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureReferenceGraph? referenceGraph,
        Func<Type, bool> traversePredicate,
        ArchitectureRoleIndex? roleIndex)
    {
        string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
        List<string> forbiddenRefs = new();
        HashSet<string> matchedPrefixes = new(StringComparer.Ordinal);
        List<IReadOnlyCollection<string>> paths = new();

        IEnumerable<(Type referenced, List<Type> path)> transitiveReferences = referenceGraph != null
            ? referenceGraph.GetTransitiveReferencedTypes(type, traversePredicate)
            : ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type, traversePredicate);

        foreach (var (referenced, path) in transitiveReferences)
        {
            CollectForbiddenTransitiveReference(
                referenced, path, forbiddenLayer, allowedTypeFullNames, executionContext, sourceFullName,
                forbiddenRefs, matchedPrefixes, paths, roleIndex);
        }

        if (forbiddenRefs.Count == 0)
        {
            return null;
        }

        var paired = forbiddenRefs
            .Zip(paths, (rf, p) => (refName: rf, path: p))
            .OrderBy(x => x.refName, StringComparer.Ordinal)
            .ToList();

        return new ArchitectureViolation(
            executionContext.ContractName,
            executionContext.ContractId,
            sourceFullName,
            ArchitectureLayerResolver.DescribeLayer(forbiddenLayer),
            paired.Select(x => x.refName).ToArray())
        {
            Payload = new ConfigurationPayload(
                DependencyPaths: paired.Select(x => x.path).ToArray()),
            MatchedNamespacePrefixes = matchedPrefixes.Count > 0
                ? matchedPrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray()
                : null
        };
    }

    private static void CollectForbiddenTransitiveReference( // NOSONAR: traversal state is intentionally explicit and mutable.
        Type referenced,
        List<Type> path,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        ArchitectureContractExecutionContext executionContext,
        string sourceFullName,
        List<string> forbiddenRefs,
        HashSet<string> matchedPrefixes,
        List<IReadOnlyCollection<string>> paths,
        ArchitectureRoleIndex? roleIndex)
    {
        string refFullName = ArchitectureTypeNames.SafeFullName(referenced);
        if (string.IsNullOrEmpty(refFullName))
        {
            return;
        }

        ArchitectureNamespaceMatch match = MatchReference(forbiddenLayer, referenced, roleIndex);
        if (!match.Matched)
        {
            return;
        }

        if (allowedTypeFullNames.Contains(refFullName))
        {
            return;
        }

        if (executionContext.IsIgnored(sourceFullName, refFullName))
        {
            return;
        }

        forbiddenRefs.Add(refFullName);
        if (!string.IsNullOrEmpty(match.MatchedNamespacePrefix))
        {
            matchedPrefixes.Add(match.MatchedNamespacePrefix);
        }

        paths.Add(path.Select(ArchitectureTypeNames.SafeFullName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToArray());
    }

    public static List<ArchitectureViolation> MergeMethodBodyViolations(
        string contractName,
        string? contractId,
        IReadOnlyList<ArchitectureViolation> roslynViolations,
        IReadOnlyList<ArchitectureViolation> ilViolations)
    {
        Dictionary<string, ArchitectureViolation> merged = new(StringComparer.Ordinal);

        foreach (ArchitectureViolation violation in roslynViolations)
        {
            foreach (string reference in violation.ForbiddenReferences)
            {
                string key = ExtractNormalizedKey(reference);
                if (merged.TryGetValue(key, out ArchitectureViolation? existing))
                {
                    List<string> combined = existing.ForbiddenReferences.Append(reference).ToList();
                    merged[key] = new ArchitectureViolation(contractName, contractId, existing.SourceType,
                        existing.ForbiddenNamespace, combined);
                }
                else
                {
                    merged[key] = new ArchitectureViolation(contractName, contractId, violation.SourceType,
                        violation.ForbiddenNamespace, new List<string> { reference });
                }
            }
        }

        foreach (ArchitectureViolation violation in ilViolations)
        {
            foreach (string reference in violation.ForbiddenReferences)
            {
                string key = ExtractNormalizedKey(reference);
                if (merged.TryGetValue(key, out ArchitectureViolation? existing))
                {
                    List<string> combined = existing.ForbiddenReferences.Append(reference).ToList();
                    merged[key] = new ArchitectureViolation(contractName, contractId, existing.SourceType,
                        existing.ForbiddenNamespace, combined);
                }
                else
                {
                    merged[key] = new ArchitectureViolation(contractName, contractId, violation.SourceType,
                        violation.ForbiddenNamespace, new List<string> { reference });
                }
            }
        }

        return merged.Values
            .OrderBy(v => v.SourceType, StringComparer.Ordinal)
            .ThenBy(v => v.ForbiddenNamespace, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsInAnyAllowedLayer(string namespaceName, IReadOnlyList<ArchitectureLayer> allowedLayers)
    {
        return allowedLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, namespaceName));
    }

    public static bool IsInAnyAllowedLayer(Type type, IReadOnlyList<ArchitectureLayer> allowedLayers, ArchitectureRoleIndex roleIndex)
    {
        return allowedLayers.Any(layer => ArchitectureLayerTypeMatcher.Matches(layer, type, roleIndex));
    }

    private static ArchitectureNamespaceMatch MatchReference(
        ArchitectureLayer layer,
        Type referenced,
        ArchitectureRoleIndex? roleIndex)
    {
        if (roleIndex == null)
        {
            return ArchitectureLayerResolver.MatchNamespace(
                layer,
                ArchitectureTypeNames.SafeNamespace(referenced));
        }

        return ArchitectureLayerTypeMatcher.Matches(layer, referenced, roleIndex)
            ? new ArchitectureNamespaceMatch(true, ArchitectureLayerResolver.DescribeLayer(layer), null)
            : new ArchitectureNamespaceMatch(false, string.Empty, null);
    }

    private static string ExtractNormalizedKey(string reference)
    {
        const string Marker = " -> ";
        int markerIndex = reference.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return reference;
        }

        string pattern = reference[..markerIndex].Trim();
        string target = reference[(markerIndex + Marker.Length)..].Trim();

        int lineEnd = target.IndexOf(' ');
        if (lineEnd > 0)
        {
            target = target[..lineEnd];
        }

        return $"{pattern} -> {target}";
    }
}
