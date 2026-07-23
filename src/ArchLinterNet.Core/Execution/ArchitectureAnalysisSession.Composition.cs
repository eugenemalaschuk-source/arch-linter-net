using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckCompositionContract(ArchitectureCompositionContract contract)
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

        IReadOnlyList<ForbiddenCallPattern> patterns =
            ArchitectureForbiddenCallMatcher.NormalizePatterns(contract.ForbiddenApis);

        string expectedCompositionBoundary = DescribeCompositionBoundary(contract);

        Dictionary<string, bool> matchCache = new(StringComparer.Ordinal);

        Type[] candidateTypes = TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
            string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;

            bool insideCompositionBoundary = IsAllowedLocation(
                actualNamespace, actualAssemblyName, allowedLayers, contract.AllowedOnlyInNamespaces, allowedAssemblyNames);

            if (insideCompositionBoundary)
            {
                continue;
            }

            string sourceType = ArchitectureTypeNames.SafeFullName(type);

            var matches = ArchitectureIlMethodBodyScanner.FindMatchDetailsForType(type, patterns, matchCache)
                .Distinct()
                .OrderBy(match => match.MatchedMember, StringComparer.Ordinal)
                .ThenBy(match => match.SourceMember, StringComparer.Ordinal);

            foreach (ArchitectureIlForbiddenCallMatch match in matches)
            {
                string matchedForbiddenApi = match.MatchedMember;
                if (executionContext.IsIgnored(
                        sourceType, matchedForbiddenApi,
                        sourceAssembly: actualAssemblyName,
                        sourceMember: match.SourceMember,
                        targetMember: matchedForbiddenApi))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceType,
                    matchedForbiddenApi,
                    new[] { matchedForbiddenApi })
                {
                    Payload = new CompositionPayload(
                        MatchedForbiddenApi: matchedForbiddenApi,
                        SourceMember: match.SourceMember,
                        ExpectedCompositionBoundary: expectedCompositionBoundary)
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private static string DescribeCompositionBoundary(ArchitectureCompositionContract contract)
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
