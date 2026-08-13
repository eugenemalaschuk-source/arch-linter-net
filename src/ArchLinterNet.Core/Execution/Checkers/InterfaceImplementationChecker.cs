using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class InterfaceImplementationChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureInterfaceImplementationContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        List<ArchitectureLayer> allowedLayers = contract.AllowedOnlyInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerName))
            .ToList();

        HashSet<string> allowedAssemblyNames = CheckerLocationAllowance.ResolveAssemblyNames(
            context, contract.AllowedOnlyInAssemblies, contract.AllowedOnlyInProjects);

        List<ArchitectureLayer> forbiddenLayers = contract.ForbiddenInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerName))
            .ToList();

        HashSet<string> forbiddenAssemblyNames = CheckerLocationAllowance.ResolveAssemblyNames(
            context, contract.ForbiddenInAssemblies, contract.ForbiddenInProjects);

        bool hasAllowedOnlyExpectation = contract.AllowedOnlyInLayers.Count > 0
            || contract.AllowedOnlyInNamespaces.Count > 0
            || contract.AllowedOnlyInProjects.Count > 0
            || contract.AllowedOnlyInAssemblies.Count > 0;

        bool hasForbiddenExpectation = contract.ForbiddenInLayers.Count > 0
            || contract.ForbiddenInNamespaces.Count > 0
            || contract.ForbiddenInProjects.Count > 0
            || contract.ForbiddenInAssemblies.Count > 0;

        string expectedAllowedOnlyLocation = CheckerLocationAllowance.DescribeLocation(
            contract.AllowedOnlyInLayers, contract.AllowedOnlyInNamespaces,
            contract.AllowedOnlyInProjects, contract.AllowedOnlyInAssemblies);
        var collectionContext = new CollectionContext(
            allowedLayers, allowedAssemblyNames, hasAllowedOnlyExpectation,
            forbiddenLayers, forbiddenAssemblyNames, hasForbiddenExpectation,
            expectedAllowedOnlyLocation, executionContext);

        Type[] candidateTypes = context.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            CollectViolationsForType(type, contract, collectionContext, violations);
        }

        return violations;
    }

    private static void CollectViolationsForType(
        Type type,
        ArchitectureInterfaceImplementationContract contract,
        CollectionContext context,
        List<ArchitectureViolation> violations)
    {
        string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
        string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;
        string actualLocationDescription = $"namespace:{actualNamespace} (assembly {actualAssemblyName})";

        bool misplaced = context.HasAllowedOnlyExpectation && !CheckerLocationAllowance.IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.AllowedLayers, contract.AllowedOnlyInNamespaces,
            context.AllowedAssemblyNames);

        bool forbidden = context.HasForbiddenExpectation && CheckerLocationAllowance.IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.ForbiddenLayers, contract.ForbiddenInNamespaces,
            context.ForbiddenAssemblyNames);

        if (!misplaced && !forbidden)
        {
            return;
        }

        string sourceType = ArchitectureTypeNames.SafeFullName(type);

        var matches = ArchitectureTypeRelationshipScanner
                .GetImplementedInterfaceMatches(type, contract.Interfaces, contract.InterfacePrefixes)
            .OrderBy(m => m.TypeName, StringComparer.Ordinal)
            .ThenBy(m => m.AssemblyName, StringComparer.Ordinal);

        foreach (ArchitectureTypeRelationshipMatch match in matches)
        {
            if (context.ExecutionContext.IsIgnored(
                    sourceType,
                    match.TypeName,
                    sourceAssembly: actualAssemblyName,
                    targetAssembly: match.AssemblyName,
                    targetType: match.TypeName,
                    targetMember: match.TypeName))
            {
                continue;
            }

            string implementationKind = forbidden ? "forbidden" : "misplaced";
            string? expectedImplementationLocation = misplaced && !forbidden ? context.ExpectedAllowedOnlyLocation : null;

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                sourceType,
                match.TypeName,
                new[] { actualLocationDescription })
            {
                Payload = new InterfaceImplementationPayload(
                    MatchedInterface: match.TypeName,
                    ImplementationKind: implementationKind,
                    ActualImplementationLocation: actualLocationDescription,
                    ExpectedImplementationLocation: expectedImplementationLocation)
            });
        }
    }

    private sealed record CollectionContext(
        List<ArchitectureLayer> AllowedLayers,
        HashSet<string> AllowedAssemblyNames,
        bool HasAllowedOnlyExpectation,
        List<ArchitectureLayer> ForbiddenLayers,
        HashSet<string> ForbiddenAssemblyNames,
        bool HasForbiddenExpectation,
        string ExpectedAllowedOnlyLocation,
        ArchitectureContractExecutionContext ExecutionContext);
}
