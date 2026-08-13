using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class TypePlacementChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureTypePlacementContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        List<ArchitectureLayer> allowedLayers = contract.MustResideInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerName))
            .ToList();

        HashSet<string> allowedAssemblyNames = CheckerLocationAllowance.ResolveAssemblyNames(
            context, contract.MustResideInAssemblies, contract.MustResideInProjects);

        bool hasPlacementExpectation = contract.MustResideInLayers.Count > 0
            || contract.MustResideInNamespaces.Count > 0
            || contract.MustResideInProjects.Count > 0
            || contract.MustResideInAssemblies.Count > 0;

        string expectedLocationDescription = CheckerLocationAllowance.DescribeLocation(
            contract.MustResideInLayers, contract.MustResideInNamespaces,
            contract.MustResideInProjects, contract.MustResideInAssemblies);
        string expectedNameDescription = ArchitectureNameConventionMatcher.Describe(
            contract.RequiredNameSuffix, contract.RequiredNamePrefix,
            contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix);
        var collectionContext = new CollectionContext(
            allowedLayers, allowedAssemblyNames, hasPlacementExpectation,
            expectedLocationDescription, expectedNameDescription, executionContext);

        bool[] exclusionMatched = new bool[contract.ExcludeTypesMatching.Count];
        Type[] includedTypes = context.TypeIndex.AllTypes()
            .Where(type => ArchitectureTypeRoleMatcher.Matches(type, contract.TypesMatching, context.Document, contract.Name))
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        // Inclusion is captured before subtraction: an excluded type still proves the positive
        // selector had a candidate, making effective scope observable without rerunning matching.
        context.RecordSubtractiveMatcherParticipation(
            contract, "types_matching", null, includedTypes.Length > 0,
            kind: ArchitectureSelectorParticipationKind.Inclusion);

        Type[] candidateTypes = includedTypes
            .Where(type => !IsExcludedType(type, contract, context, exclusionMatched))
            .ToArray();

        for (int index = 0; index < contract.ExcludeTypesMatching.Count; index++)
        {
            context.RecordSubtractiveMatcherParticipation(
                contract, "exclude_types_matching", index, exclusionMatched[index]);
        }

        foreach (Type type in candidateTypes)
        {
            TryAddViolation(type, contract, collectionContext, violations);
        }

        return violations;
    }

    // Records which exclude_types_matching[i] item actually matched this type, rather than only
    // whether any of them did, so RecordSubtractiveMatcherParticipation can report per-item
    // matched/stale evidence instead of collapsing every exclusion into one boolean.
    private static bool IsExcludedType(
        Type type,
        ArchitectureTypePlacementContract contract,
        ArchitectureCheckerContext context,
        bool[] exclusionMatched)
    {
        bool excluded = false;
        for (int index = 0; index < contract.ExcludeTypesMatching.Count; index++)
        {
            if (!ArchitectureTypeRoleMatcher.Matches(
                    type, contract.ExcludeTypesMatching[index], context.Document, contract.Name))
            {
                continue;
            }

            exclusionMatched[index] = true;
            excluded = true;
        }

        return excluded;
    }

    private static void TryAddViolation(
        Type type,
        ArchitectureTypePlacementContract contract,
        CollectionContext context,
        List<ArchitectureViolation> violations)
    {
        string sourceType = ArchitectureTypeNames.SafeFullName(type);
        string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
        string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;

        bool placementOk = !context.HasPlacementExpectation || CheckerLocationAllowance.IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.AllowedLayers, contract.MustResideInNamespaces,
            context.AllowedAssemblyNames);

        bool namingOk = ArchitectureNameConventionMatcher.Matches(
            type.Name, contract.RequiredNameSuffix, contract.RequiredNamePrefix,
            contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix);

        if (placementOk && namingOk)
        {
            return;
        }

        string? expectedTypeLocation = !placementOk ? context.ExpectedLocationDescription : null;
        string? actualTypeLocation = !placementOk ? $"namespace:{actualNamespace} (assembly {actualAssemblyName})" : null;
        string? expectedTypeName = !namingOk ? context.ExpectedNameDescription : null;
        string? actualTypeName = !namingOk ? type.Name : null;

        string forbiddenReference = actualTypeLocation ?? actualTypeName ?? sourceType;

        if (context.ExecutionContext.IsIgnored(
                sourceType,
                forbiddenReference,
                sourceAssembly: actualAssemblyName,
                targetMember: forbiddenReference))
        {
            return;
        }

        violations.Add(new ArchitectureViolation(
            contract.Name,
            contract.Id,
            sourceType,
            expectedTypeLocation ?? expectedTypeName ?? string.Empty,
            new[] { forbiddenReference })
        {
            Payload = new TypePlacementPayload(
                ExpectedTypeLocation: expectedTypeLocation,
                ActualTypeLocation: actualTypeLocation,
                ExpectedTypeName: expectedTypeName,
                ActualTypeName: actualTypeName)
        });
    }

    private sealed record CollectionContext(
        List<ArchitectureLayer> AllowedLayers,
        HashSet<string> AllowedAssemblyNames,
        bool HasPlacementExpectation,
        string ExpectedLocationDescription,
        string ExpectedNameDescription,
        ArchitectureContractExecutionContext ExecutionContext);
}
