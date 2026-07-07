using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckInterfaceImplementationContract(ArchitectureInterfaceImplementationContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureLayer> allowedLayers = contract.AllowedOnlyInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerName))
            .ToList();

        HashSet<string> allowedAssemblyNames = new(contract.AllowedOnlyInAssemblies, StringComparer.Ordinal);
        foreach (string resolvedAssemblyName in ResolveProjectAssemblyNames(contract.AllowedOnlyInProjects))
        {
            allowedAssemblyNames.Add(resolvedAssemblyName);
        }

        List<ArchitectureLayer> forbiddenLayers = contract.ForbiddenInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerName))
            .ToList();

        HashSet<string> forbiddenAssemblyNames = new(contract.ForbiddenInAssemblies, StringComparer.Ordinal);
        foreach (string resolvedAssemblyName in ResolveProjectAssemblyNames(contract.ForbiddenInProjects))
        {
            forbiddenAssemblyNames.Add(resolvedAssemblyName);
        }

        bool hasAllowedOnlyExpectation = contract.AllowedOnlyInLayers.Count > 0
            || contract.AllowedOnlyInNamespaces.Count > 0
            || contract.AllowedOnlyInProjects.Count > 0
            || contract.AllowedOnlyInAssemblies.Count > 0;

        bool hasForbiddenExpectation = contract.ForbiddenInLayers.Count > 0
            || contract.ForbiddenInNamespaces.Count > 0
            || contract.ForbiddenInProjects.Count > 0
            || contract.ForbiddenInAssemblies.Count > 0;

        string expectedAllowedOnlyLocation = DescribeAllowedOnlyImplementationLocation(contract);

        Type[] candidateTypes = TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
            string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string actualLocationDescription = $"namespace:{actualNamespace} (assembly {actualAssemblyName})";

            bool misplaced = hasAllowedOnlyExpectation && !IsAllowedLocation(
                actualNamespace, actualAssemblyName, allowedLayers, contract.AllowedOnlyInNamespaces, allowedAssemblyNames);

            bool forbidden = hasForbiddenExpectation && IsAllowedLocation(
                actualNamespace, actualAssemblyName, forbiddenLayers, contract.ForbiddenInNamespaces, forbiddenAssemblyNames);

            if (!misplaced && !forbidden)
            {
                continue;
            }

            string sourceType = ArchitectureTypeNames.SafeFullName(type);

            var matches = ArchitectureTypeRelationshipScanner
                .GetImplementedInterfaceMatches(type, contract.Interfaces, contract.InterfacePrefixes)
                .OrderBy(m => m, StringComparer.Ordinal);

            foreach (string matchedInterface in matches)
            {
                if (executionContext.IsIgnored(sourceType, matchedInterface))
                {
                    continue;
                }

                string implementationKind = forbidden ? "forbidden" : "misplaced";
                string? expectedImplementationLocation = misplaced && !forbidden ? expectedAllowedOnlyLocation : null;

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceType,
                    matchedInterface,
                    new[] { actualLocationDescription })
                {
                    MatchedInterface = matchedInterface,
                    ImplementationKind = implementationKind,
                    ActualImplementationLocation = actualLocationDescription,
                    ExpectedImplementationLocation = expectedImplementationLocation
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private static string DescribeAllowedOnlyImplementationLocation(ArchitectureInterfaceImplementationContract contract)
    {
        List<string> parts = new();
        if (contract.AllowedOnlyInLayers.Count > 0)
        {
            parts.Add($"layers: [{string.Join(", ", contract.AllowedOnlyInLayers)}]");
        }

        if (contract.AllowedOnlyInNamespaces.Count > 0)
        {
            parts.Add($"namespaces: [{string.Join(", ", contract.AllowedOnlyInNamespaces)}]");
        }

        if (contract.AllowedOnlyInProjects.Count > 0)
        {
            parts.Add($"projects: [{string.Join(", ", contract.AllowedOnlyInProjects)}]");
        }

        if (contract.AllowedOnlyInAssemblies.Count > 0)
        {
            parts.Add($"assemblies: [{string.Join(", ", contract.AllowedOnlyInAssemblies)}]");
        }

        return string.Join("; ", parts);
    }
}
