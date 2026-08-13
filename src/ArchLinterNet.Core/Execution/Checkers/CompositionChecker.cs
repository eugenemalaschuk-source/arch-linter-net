using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class CompositionChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureCompositionContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        List<ArchitectureLayer> allowedLayers = contract.AllowedOnlyInLayers
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerName))
            .ToList();

        HashSet<string> allowedAssemblyNames = CheckerLocationAllowance.ResolveAssemblyNames(
            context, contract.AllowedOnlyInAssemblies, contract.AllowedOnlyInProjects);

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

        Type[] candidateTypes = context.TypeIndex.AllTypes()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in candidateTypes)
        {
            string actualNamespace = ArchitectureTypeNames.SafeNamespace(type);
            string actualAssemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string sourceType = ArchitectureTypeNames.SafeFullName(type);

            bool insideCompositionBoundary = CheckerLocationAllowance.IsAllowedLocation(
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

        return violations;
    }

    private static string DescribeCompositionBoundary(ArchitectureCompositionContract contract)
    {
        string location = CheckerLocationAllowance.DescribeLocation(
            contract.AllowedOnlyInLayers, contract.AllowedOnlyInNamespaces,
            contract.AllowedOnlyInProjects, contract.AllowedOnlyInAssemblies);

        if (contract.AllowedOnlyInTypes.Count == 0)
        {
            return location;
        }

        string types = string.Join(", ", contract.AllowedOnlyInTypes.Select(t => $"{t.Assembly}:{t.Type}"));
        string typesPart = $"types: [{types}]";
        return location.Length == 0 ? typesPart : $"{location}; {typesPart}";
    }
}
