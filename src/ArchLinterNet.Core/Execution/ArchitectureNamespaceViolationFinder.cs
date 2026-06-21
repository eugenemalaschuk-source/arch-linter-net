using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureNamespaceViolationFinder
{
    public static IEnumerable<ArchitectureViolation> FindNamespaceViolations(
        string contractName,
        string? contractId,
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker = null,
        string? contractGroup = null,
        List<ArchitectureBaselineCandidate>? baselineCandidates = null)
    {
        return sourceTypes
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                var forbiddenMatches = ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(reference => new
                    {
                        Reference = reference,
                        Match = ArchitectureLayerResolver.MatchNamespace(forbiddenLayer,
                            ArchitectureTypeNames.SafeNamespace(reference))
                    })
                    .Where(x => x.Match.Matched)
                    .Select(x => new
                    {
                        FullName = ArchitectureTypeNames.SafeFullName(x.Reference),
                        x.Match.MatchedNamespacePrefix
                    })
                    .Where(x => !string.IsNullOrEmpty(x.FullName))
                    .Where(x => !allowedTypeFullNames.Contains(x.FullName))
                    .Where(x =>
                    {
                        bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceFullName, x.FullName,
                            ignoredViolations, usageTracker);
                        if (!ignored && contractGroup != null && baselineCandidates != null)
                        {
                            baselineCandidates.Add(new ArchitectureBaselineCandidate(contractGroup, contractId, sourceFullName, x.FullName));
                        }

                        return !ignored;
                    })
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
                    contractName, contractId, sourceFullName,
                    ArchitectureLayerResolver.DescribeLayer(forbiddenLayer), forbiddenRefs)
                {
                    MatchedNamespacePrefixes = matchedPrefixes.Length > 0 ? matchedPrefixes : null
                };
            })
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToArray();
    }

    public static IEnumerable<ArchitectureViolation> FindTransitiveNamespaceViolations(
        string contractName,
        string? contractId,
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        IReadOnlyCollection<Assembly> targetAssemblies,
        ArchitectureIgnoreUsageTracker? usageTracker = null,
        string? contractGroup = null,
        List<ArchitectureBaselineCandidate>? baselineCandidates = null)
    {
        HashSet<Assembly> assemblySet = targetAssemblies.ToHashSet();
        Func<Type, bool> traversePredicate = t => assemblySet.Contains(t.Assembly);

        return sourceTypes
            .OrderBy(type => ArchitectureTypeNames.SafeFullName(type), StringComparer.Ordinal)
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                List<string> forbiddenRefs = new();
                HashSet<string> matchedPrefixes = new(StringComparer.Ordinal);
                List<IReadOnlyCollection<string>> paths = new();

                foreach (var (referenced, path) in ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type, traversePredicate))
                {
                    string refFullName = ArchitectureTypeNames.SafeFullName(referenced);
                    if (string.IsNullOrEmpty(refFullName))
                    {
                        continue;
                    }

                    ArchitectureNamespaceMatch match = ArchitectureLayerResolver.MatchNamespace(forbiddenLayer,
                        ArchitectureTypeNames.SafeNamespace(referenced));
                    if (!match.Matched)
                    {
                        continue;
                    }

                    if (allowedTypeFullNames.Contains(refFullName))
                    {
                        continue;
                    }

                    if (ArchitectureIgnoreMatcher.IsIgnored(sourceFullName, refFullName, ignoredViolations, usageTracker))
                    {
                        continue;
                    }

                    if (contractGroup != null && baselineCandidates != null)
                    {
                        baselineCandidates.Add(new ArchitectureBaselineCandidate(contractGroup, contractId, sourceFullName, refFullName));
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

                if (forbiddenRefs.Count == 0)
                {
                    return null;
                }

                var paired = forbiddenRefs
                    .Zip(paths, (rf, p) => (refName: rf, path: p))
                    .OrderBy(x => x.refName, StringComparer.Ordinal)
                    .ToList();

                return new ArchitectureViolation(
                    contractName,
                    contractId,
                    sourceFullName,
                    ArchitectureLayerResolver.DescribeLayer(forbiddenLayer),
                    paired.Select(x => x.refName).ToArray())
                {
                    DependencyPaths = paired.Select(x => x.path).ToArray(),
                    MatchedNamespacePrefixes = matchedPrefixes.Count > 0
                        ? matchedPrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray()
                        : null
                };
            })
            .Where(violation => violation != null)!;
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
