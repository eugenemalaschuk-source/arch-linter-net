using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal sealed class InheritanceChecker
{
    public List<ArchitectureViolation> Check(
        ArchitectureInheritanceContract contract,
        ArchitectureContractDocument document,
        ArchitectureTypeIndex typeIndex,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        List<ArchitectureLayer> sourceLayers = contract.SourceLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(document, contract.Name, layerName))
            .ToList();

        string sourceSurfaceDescription = DescribeInheritanceSourceSurface(contract);

        Type[] candidateTypes = typeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);

            bool inSourceSurface = sourceLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, actualNamespace))
                || contract.SourceNamespaces.Any(prefix => ArchitectureLayerResolver.MatchesPrefix(actualNamespace, prefix));

            if (!inSourceSurface)
            {
                continue;
            }

            string sourceType = ArchitectureTypeNames.SafeFullName(type);

            var matches = ArchitectureTypeRelationshipScanner
                .GetForbiddenBaseTypeMatches(type, contract.ForbiddenBaseTypes, contract.ForbiddenBaseTypePrefixes)
                .OrderBy(m => m, StringComparer.Ordinal);

            foreach (string matchedBaseType in matches)
            {
                if (executionContext.IsIgnored(sourceType, matchedBaseType))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceType,
                    matchedBaseType,
                    new[] { matchedBaseType })
                {
                    Payload = new InheritancePayload(
                        ForbiddenBaseType: matchedBaseType,
                        InheritanceSourceSurface: sourceSurfaceDescription)
                });
            }
        }

        return violations;
    }

    private static string DescribeInheritanceSourceSurface(ArchitectureInheritanceContract contract)
    {
        List<string> parts = new();
        if (contract.SourceLayers.Count > 0)
        {
            parts.Add($"layers: [{string.Join(", ", contract.SourceLayers)}]");
        }

        if (contract.SourceNamespaces.Count > 0)
        {
            parts.Add($"namespaces: [{string.Join(", ", contract.SourceNamespaces)}]");
        }

        return string.Join("; ", parts);
    }
}
