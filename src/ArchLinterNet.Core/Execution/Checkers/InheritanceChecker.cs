using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class InheritanceChecker
{
    public static List<ArchitectureViolation> Check(
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
            string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;

            var matches = ArchitectureTypeRelationshipScanner
                .GetForbiddenBaseTypeMatches(type, contract.ForbiddenBaseTypes, contract.ForbiddenBaseTypePrefixes)
                .OrderBy(m => m.TypeName, StringComparer.Ordinal)
                .ThenBy(m => m.AssemblyName, StringComparer.Ordinal);

            foreach (ArchitectureTypeRelationshipMatch match in matches)
            {
                if (executionContext.IsIgnored(
                        sourceType,
                        match.TypeName,
                        sourceAssembly: sourceAssembly,
                        targetAssembly: match.AssemblyName,
                        targetType: match.TypeName,
                        targetMember: match.TypeName))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceType,
                    match.TypeName,
                    new[] { match.TypeName })
                {
                    Payload = new InheritancePayload(
                        ForbiddenBaseType: match.TypeName,
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
