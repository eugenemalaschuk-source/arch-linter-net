using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckTypePlacementContract(ArchitectureTypePlacementContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureLayer> allowedLayers = contract.MustResideInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerName))
            .ToList();

        HashSet<string> allowedAssemblyNames = new(contract.MustResideInAssemblies, StringComparer.Ordinal);
        foreach (string resolvedAssemblyName in ResolveProjectAssemblyNames(contract.MustResideInProjects))
        {
            allowedAssemblyNames.Add(resolvedAssemblyName);
        }

        bool hasPlacementExpectation = contract.MustResideInLayers.Count > 0
            || contract.MustResideInNamespaces.Count > 0
            || contract.MustResideInProjects.Count > 0
            || contract.MustResideInAssemblies.Count > 0;

        string expectedLocationDescription = DescribeExpectedLocation(contract);
        string expectedNameDescription = DescribeExpectedName(contract);
        var context = new TypePlacementCollectionContext(
            allowedLayers, allowedAssemblyNames, hasPlacementExpectation,
            expectedLocationDescription, expectedNameDescription, executionContext);

        Type[] candidateTypes = TypeIndex.AllTypes()
            .Where(type => ArchitectureTypeRoleMatcher.Matches(type, contract.TypesMatching, Document, contract.Name))
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            TryAddTypePlacementViolation(
                type,
                contract,
                context,
                violations);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private static void TryAddTypePlacementViolation(
        Type type,
        ArchitectureTypePlacementContract contract,
        TypePlacementCollectionContext context,
        List<ArchitectureViolation> violations)
    {
        string sourceType = ArchitectureTypeNames.SafeFullName(type);
        string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
        string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;

        bool placementOk = !context.HasPlacementExpectation || IsAllowedLocation(
            actualNamespace, actualAssemblyName, context.AllowedLayers, contract.MustResideInNamespaces, context.AllowedAssemblyNames);

        bool namingOk = IsNamingSatisfied(type.Name, contract);

        if (placementOk && namingOk)
        {
            return;
        }

        string? expectedTypeLocation = !placementOk ? context.ExpectedLocationDescription : null;
        string? actualTypeLocation = !placementOk ? $"namespace:{actualNamespace} (assembly {actualAssemblyName})" : null;
        string? expectedTypeName = !namingOk ? context.ExpectedNameDescription : null;
        string? actualTypeName = !namingOk ? type.Name : null;

        string forbiddenReference = actualTypeLocation ?? actualTypeName ?? sourceType;

        if (context.ExecutionContext.IsIgnored(sourceType, forbiddenReference))
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

    private sealed record TypePlacementCollectionContext(
        List<ArchitectureLayer> AllowedLayers,
        HashSet<string> AllowedAssemblyNames,
        bool HasPlacementExpectation,
        string ExpectedLocationDescription,
        string ExpectedNameDescription,
        ArchitectureContractExecutionContext ExecutionContext);

    private static bool IsAllowedLocation(
        string actualNamespace,
        string actualAssemblyName,
        IReadOnlyList<ArchitectureLayer> allowedLayers,
        IReadOnlyList<string> allowedNamespacePrefixes,
        IReadOnlySet<string> allowedAssemblyNames)
    {
        if (allowedLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, actualNamespace)))
        {
            return true;
        }

        if (allowedNamespacePrefixes.Any(prefix => ArchitectureLayerResolver.MatchesPrefix(actualNamespace, prefix)))
        {
            return true;
        }

        return allowedAssemblyNames.Contains(actualAssemblyName);
    }

    private static bool IsNamingSatisfied(string typeName, ArchitectureTypePlacementContract contract)
    {
        if (!string.IsNullOrEmpty(contract.RequiredNameSuffix)
            && !typeName.EndsWith(contract.RequiredNameSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(contract.RequiredNamePrefix)
            && !typeName.StartsWith(contract.RequiredNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
            && typeName.EndsWith(contract.ForbiddenNameSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(contract.ForbiddenNamePrefix)
            && typeName.StartsWith(contract.ForbiddenNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string DescribeExpectedLocation(ArchitectureTypePlacementContract contract)
    {
        List<string> parts = new();
        if (contract.MustResideInLayers.Count > 0)
        {
            parts.Add($"layers: [{string.Join(", ", contract.MustResideInLayers)}]");
        }

        if (contract.MustResideInNamespaces.Count > 0)
        {
            parts.Add($"namespaces: [{string.Join(", ", contract.MustResideInNamespaces)}]");
        }

        if (contract.MustResideInProjects.Count > 0)
        {
            parts.Add($"projects: [{string.Join(", ", contract.MustResideInProjects)}]");
        }

        if (contract.MustResideInAssemblies.Count > 0)
        {
            parts.Add($"assemblies: [{string.Join(", ", contract.MustResideInAssemblies)}]");
        }

        return string.Join("; ", parts);
    }

    private static string DescribeExpectedName(ArchitectureTypePlacementContract contract)
    {
        List<string> parts = new();
        if (!string.IsNullOrEmpty(contract.RequiredNameSuffix))
        {
            parts.Add($"required_suffix: {contract.RequiredNameSuffix}");
        }

        if (!string.IsNullOrEmpty(contract.RequiredNamePrefix))
        {
            parts.Add($"required_prefix: {contract.RequiredNamePrefix}");
        }

        if (!string.IsNullOrEmpty(contract.ForbiddenNameSuffix))
        {
            parts.Add($"forbidden_suffix: {contract.ForbiddenNameSuffix}");
        }

        if (!string.IsNullOrEmpty(contract.ForbiddenNamePrefix))
        {
            parts.Add($"forbidden_prefix: {contract.ForbiddenNamePrefix}");
        }

        return string.Join("; ", parts);
    }

    // "Project" residency is resolved to assembly-name equivalence via project discovery: there is
    // no Type -> .csproj mapping anywhere in this codebase (a project maps 1:1 to a single assembly
    // name). A project name that doesn't match any discovered project contributes no assembly name,
    // which is fail-closed (never widens what's allowed), consistent with how other allow-only
    // contracts treat an unresolvable name.
    private IEnumerable<string> ResolveProjectAssemblyNames(IReadOnlyList<string> projectNames)
    {
        if (projectNames.Count == 0)
        {
            yield break;
        }

        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        HashSet<string> requestedProjectNames = new(projectNames, StringComparer.Ordinal);

        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            string projectFileName = Path.GetFileNameWithoutExtension(project.Path);
            if (requestedProjectNames.Contains(projectFileName))
            {
                yield return project.AssemblyName;
            }
        }
    }
}
