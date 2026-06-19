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
        IReadOnlyCollection<ArchitectureIgnoredViolation> ignoredViolations)
    {
        return sourceTypes
            .Select(type => new ArchitectureViolation(
                contractName,
                contractId,
                ArchitectureTypeNames.SafeFullName(type),
                ArchitectureLayerResolver.DescribeLayer(forbiddenLayer),
                ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Where(reference => ArchitectureLayerResolver.MatchesNamespace(forbiddenLayer,
                        ArchitectureTypeNames.SafeNamespace(reference)))
                    .Select(ArchitectureTypeNames.SafeFullName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Where(name => !allowedTypeFullNames.Contains(name))
                    .Where(name =>
                        !ArchitectureIgnoreMatcher.IsIgnored(ArchitectureTypeNames.SafeFullName(type), name,
                            ignoredViolations))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray()))
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToArray();
    }

    public static IEnumerable<ArchitectureViolation> FindTransitiveNamespaceViolations(
        string contractName,
        string? contractId,
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        IReadOnlyCollection<ArchitectureIgnoredViolation> ignoredViolations)
    {
        return sourceTypes
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                List<string> forbiddenRefs = new();
                List<IReadOnlyCollection<string>> paths = new();

                foreach (var (referenced, path) in ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type))
                {
                    string refFullName = ArchitectureTypeNames.SafeFullName(referenced);
                    if (string.IsNullOrEmpty(refFullName))
                    {
                        continue;
                    }

                    if (!ArchitectureLayerResolver.MatchesNamespace(forbiddenLayer,
                            ArchitectureTypeNames.SafeNamespace(referenced)))
                    {
                        continue;
                    }

                    if (allowedTypeFullNames.Contains(refFullName))
                    {
                        continue;
                    }

                    if (ArchitectureIgnoreMatcher.IsIgnored(sourceFullName, refFullName, ignoredViolations))
                    {
                        continue;
                    }

                    forbiddenRefs.Add(refFullName);
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
                    DependencyPaths = paired.Select(x => x.path).ToArray()
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

    public static bool IsInAnyAllowedLayer(string typeName, IReadOnlyList<ArchitectureLayer> allowedLayers)
    {
        return allowedLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, typeName));
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
