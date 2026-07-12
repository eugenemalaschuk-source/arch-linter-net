using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
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
        var context = new InterfaceImplementationCollectionContext(
            allowedLayers, allowedAssemblyNames, hasAllowedOnlyExpectation,
            forbiddenLayers, forbiddenAssemblyNames, hasForbiddenExpectation,
            expectedAllowedOnlyLocation, executionContext);

        Type[] candidateTypes = TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            CollectInterfaceImplementationViolationsForType(
                type,
                contract,
                context,
                violations);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private static void CollectInterfaceImplementationViolationsForType(
        Type type,
        ArchitectureInterfaceImplementationContract contract,
        InterfaceImplementationCollectionContext context,
        List<ArchitectureViolation> violations)
    {
        string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
        string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;
        string actualLocationDescription = $"namespace:{actualNamespace} (assembly {actualAssemblyName})";

        bool misplaced = context.HasAllowedOnlyExpectation && !IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.AllowedLayers, contract.AllowedOnlyInNamespaces, context.AllowedAssemblyNames);

        bool forbidden = context.HasForbiddenExpectation && IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.ForbiddenLayers, contract.ForbiddenInNamespaces, context.ForbiddenAssemblyNames);

        if (!misplaced && !forbidden)
        {
            return;
        }

        string sourceType = ArchitectureTypeNames.SafeFullName(type);

        var matches = ArchitectureTypeRelationshipScanner
            .GetImplementedInterfaceMatches(type, contract.Interfaces, contract.InterfacePrefixes)
            .OrderBy(m => m, StringComparer.Ordinal);

        foreach (string matchedInterface in matches)
        {
            if (context.ExecutionContext.IsIgnored(sourceType, matchedInterface))
            {
                continue;
            }

            string implementationKind = forbidden ? "forbidden" : "misplaced";
            string? expectedImplementationLocation = misplaced && !forbidden ? context.ExpectedAllowedOnlyLocation : null;

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                sourceType,
                matchedInterface,
                new[] { actualLocationDescription })
            {
                Payload = new InterfaceImplementationPayload(
                    MatchedInterface: matchedInterface,
                    ImplementationKind: implementationKind,
                    ActualImplementationLocation: actualLocationDescription,
                    ExpectedImplementationLocation: expectedImplementationLocation)
            });
        }
    }

    private sealed record InterfaceImplementationCollectionContext(
        List<ArchitectureLayer> AllowedLayers,
        HashSet<string> AllowedAssemblyNames,
        bool HasAllowedOnlyExpectation,
        List<ArchitectureLayer> ForbiddenLayers,
        HashSet<string> ForbiddenAssemblyNames,
        bool HasForbiddenExpectation,
        string ExpectedAllowedOnlyLocation,
        ArchitectureContractExecutionContext ExecutionContext);

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
