using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckInheritanceContract(ArchitectureInheritanceContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureLayer> sourceLayers = contract.SourceLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerName))
            .ToList();

        string sourceSurfaceDescription = DescribeInheritanceSourceSurface(contract);

        Type[] candidateTypes = TypeIndex.AllTypes()
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
                    ForbiddenBaseType = matchedBaseType,
                    InheritanceSourceSurface = sourceSurfaceDescription
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
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
