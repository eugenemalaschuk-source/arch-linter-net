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

        // Direct assembly+type identity pairs — narrower than allowedAssemblyNames (which allows
        // every type in the assembly). Keyed as "assembly|type" so a single global/top-level type
        // (e.g. one host's Program) can be the composition boundary without allowing the rest of
        // its assembly or namespace. See ArchitectureCompositionTypeSelector.
        HashSet<string> allowedAssemblyTypePairs = new(
            contract.AllowedOnlyInTypes.Select(selector => $"{selector.Assembly}|{selector.Type}"),
            StringComparer.Ordinal);

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
            string sourceType = ArchitectureTypeNames.SafeFullName(type);

            bool insideCompositionBoundary = IsAllowedLocation(
                    actualNamespace, actualAssemblyName, allowedLayers, contract.AllowedOnlyInNamespaces, allowedAssemblyNames)
                || allowedAssemblyTypePairs.Contains($"{actualAssemblyName}|{sourceType}");

            if (insideCompositionBoundary)
            {
                continue;
            }

            // IMPORTANT: do not Distinct() the raw IL matches before IsIgnored — each raw call site
            // (even one with an identical (method, pattern, matchedMember) shape to another call site
            // in the same method) must independently reach IsIgnored so the occurrence counter/baseline
            // candidate collection sees every distinct occurrence. Deduping first would collapse two
            // genuinely distinct forbidden-call occurrences into a single check, so baselining the first
            // would silently suppress the second too. Dedup for the reported violation *list* happens
            // after, matching the "at most one violation per (type, source member, matched API) tuple"
            // diagnostic contract without weakening occurrence discrimination underneath it.
            var rawMatches = ArchitectureIlMethodBodyScanner.FindMatchDetailsForType(type, patterns, matchCache)
                .OrderBy(match => match.MatchedMember, StringComparer.Ordinal)
                .ThenBy(match => match.SourceMember, StringComparer.Ordinal);

            HashSet<(string SourceMember, string MatchedApi)> reportedTuples = new();

            foreach (ArchitectureIlForbiddenCallMatch match in rawMatches)
            {
                string matchedForbiddenApi = match.MatchedMember;
                bool ignored = executionContext.IsIgnored(
                    sourceType, matchedForbiddenApi,
                    sourceAssembly: actualAssemblyName,
                    sourceMember: match.SourceMember,
                    targetMember: matchedForbiddenApi);

                if (ignored || !reportedTuples.Add((match.SourceMember, matchedForbiddenApi)))
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
                        SourceAssembly: actualAssemblyName,
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

        if (contract.AllowedOnlyInTypes.Count > 0)
        {
            string types = string.Join(", ", contract.AllowedOnlyInTypes.Select(t => $"{t.Assembly}:{t.Type}"));
            parts.Add($"types: [{types}]");
        }

        return string.Join("; ", parts);
    }
}
