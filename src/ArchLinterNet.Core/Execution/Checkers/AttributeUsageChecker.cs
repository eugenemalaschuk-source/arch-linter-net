using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class AttributeUsageChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureAttributeUsageContract contract,
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
        ArchitectureAttributeUsageContract contract,
        CollectionContext context,
        List<ArchitectureViolation> violations)
    {
        string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
        string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;
        string actualLocationDescription = $"namespace:{actualNamespace} (assembly {actualAssemblyName})";

        var matches = ArchitectureAttributeUsageScanner.GetMatches(type, contract.Attributes, contract.AttributePrefixes)
            .OrderBy(m => m.SourceIdentifier, StringComparer.Ordinal)
            .ThenBy(m => m.MatchedAttribute, StringComparer.Ordinal);

        foreach (var match in matches)
        {
            bool misplaced = context.HasAllowedOnlyExpectation && !CheckerLocationAllowance.IsAllowedLocation(
                actualNamespace, actualAssemblyName, context.AllowedLayers, contract.AllowedOnlyInNamespaces,
                context.AllowedAssemblyNames);

            bool forbidden = context.HasForbiddenExpectation && CheckerLocationAllowance.IsAllowedLocation(
                actualNamespace, actualAssemblyName, context.ForbiddenLayers, contract.ForbiddenInNamespaces,
                context.ForbiddenAssemblyNames);

            if (!misplaced && !forbidden)
            {
                continue;
            }

            if (context.ExecutionContext.IsIgnored(
                    match.SourceIdentifier,
                    match.MatchedAttribute,
                    sourceAssembly: actualAssemblyName,
                    targetAssembly: match.TargetAssembly,
                    targetType: match.MatchedAttribute,
                    targetMember: match.MatchedAttribute))
            {
                continue;
            }

            string attributeUsageKind = forbidden ? "forbidden" : "misplaced";
            string? expectedAttributeLocation = misplaced && !forbidden ? context.ExpectedAllowedOnlyLocation : null;

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                match.SourceIdentifier,
                match.MatchedAttribute,
                new[] { actualLocationDescription })
            {
                Payload = new AttributeUsagePayload(
                    MatchedAttribute: match.MatchedAttribute,
                    AttributeUsageKind: attributeUsageKind,
                    ActualAttributeLocation: actualLocationDescription,
                    ExpectedAttributeLocation: expectedAttributeLocation)
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
