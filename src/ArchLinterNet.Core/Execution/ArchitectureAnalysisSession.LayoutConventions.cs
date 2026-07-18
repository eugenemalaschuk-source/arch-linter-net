using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckLayoutConventionsContract(ArchitectureLayoutConventionContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();

        if (SourceFileFactIndex.AllFacts.All(fact => fact.SourceFilePath == null))
        {
            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Name,
                "path-based layout checks unavailable",
                new[]
                {
                    "No source-enriched declared-type facts are available for this run. " +
                    "Configure analysis.source_roots so layout convention contracts can evaluate file/folder facts."
                })
            {
                Payload = new LayoutConventionPayload(DataUnavailable: true)
            });
            return violations;
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<LayoutFileGroup> matchedGroups = CollectMatchedFileGroups(contract.FilesMatching);

        foreach (LayoutFileGroup group in matchedGroups)
        {
            EvaluateFileGroupExpectations(contract, group, executionContext, violations);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private List<LayoutFileGroup> CollectMatchedFileGroups(ArchitectureLayoutFileMatcher matcher)
    {
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byGroupKey = new(StringComparer.Ordinal);

        foreach (Type type in TypeIndex.AllTypes())
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (string.IsNullOrEmpty(fullName)
                || !SourceFileFactIndex.TryGetFact(assemblyName, fullName, out ArchitectureDeclaredTypeFact fact))
            {
                continue;
            }

            if (!MatchesFilesSelector(matcher, fact))
            {
                continue;
            }

            if (matcher.CompiledWhen != null && !EvaluateLayoutWhen(matcher, type))
            {
                continue;
            }

            string groupKey = fact.SourceFilePath ?? $"~unfiled~/{fact.AssemblyName}/{fact.FullTypeName}";
            if (!byGroupKey.TryGetValue(groupKey, out List<ArchitectureDeclaredTypeFact>? facts))
            {
                facts = new List<ArchitectureDeclaredTypeFact>();
                byGroupKey[groupKey] = facts;
            }

            facts.Add(fact);
        }

        return byGroupKey
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new LayoutFileGroup(
                entry.Value[0].SourceFilePath,
                entry.Value[0].FileNameWithoutExtension,
                entry.Value))
            .ToList();
    }

    private static bool MatchesFilesSelector(ArchitectureLayoutFileMatcher matcher, ArchitectureDeclaredTypeFact fact)
    {
        bool requiresSourceFile = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);

        if (requiresSourceFile && fact.SourceFilePath == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.FolderSegment)
            && !fact.FolderSegments.Contains(matcher.FolderSegment, StringComparer.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
            && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.FileNameSuffix)
            && (fact.FileNameWithoutExtension == null
                || !fact.FileNameWithoutExtension.EndsWith(matcher.FileNameSuffix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.FileNamePrefix)
            && (fact.FileNameWithoutExtension == null
                || !fact.FileNameWithoutExtension.StartsWith(matcher.FileNamePrefix, StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private bool EvaluateLayoutWhen(ArchitectureLayoutFileMatcher matcher, Type type)
    {
        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(ExpressionFacts.BuildSubjectFacts(type));
        string description =
            $"Layout convention files_matching at '{matcher.WhenLocation?.YamlPath}' (contract: {matcher.WhenContractName}, " +
            $"when: {matcher.When}) for type '{ArchitectureTypeNames.SafeFullName(type)}'";
        return ArchitectureExpressionFactService.Evaluate(matcher.CompiledWhen!, context, description, matcher.WhenLocation);
    }

    private void EvaluateFileGroupExpectations(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        string groupLabel = group.SourceFilePath ?? group.Facts[0].FullTypeName;

        if (!string.IsNullOrEmpty(contract.RequireTypeKind))
        {
            ArchitectureTypeKind requiredKind = ParseTypeKind(contract.RequireTypeKind);
            if (!group.Facts.Any(fact => fact.TypeKind == requiredKind))
            {
                string actualKinds = string.Join(", ", group.Facts.Select(f => f.TypeKind.ToString()).Distinct(StringComparer.Ordinal));
                AddViolation(
                    contract, executionContext, violations,
                    sourceType: groupLabel,
                    forbiddenReference: $"expected type kind '{contract.RequireTypeKind}', found: [{actualKinds}]",
                    payload: new LayoutConventionPayload(
                        MatchedFilePath: group.SourceFilePath,
                        ExpectedTypeKind: contract.RequireTypeKind,
                        ActualTypeKind: actualKinds));
            }
        }

        if (!string.IsNullOrEmpty(contract.ForbidTypeKind))
        {
            ArchitectureTypeKind forbiddenKind = ParseTypeKind(contract.ForbidTypeKind);
            foreach (ArchitectureDeclaredTypeFact fact in group.Facts.Where(f => f.TypeKind == forbiddenKind))
            {
                AddViolation(
                    contract, executionContext, violations,
                    sourceType: fact.FullTypeName,
                    forbiddenReference: $"forbidden type kind '{contract.ForbidTypeKind}'",
                    payload: new LayoutConventionPayload(
                        MatchedFilePath: group.SourceFilePath,
                        ExpectedTypeKind: $"not {contract.ForbidTypeKind}",
                        ActualTypeKind: fact.TypeKind.ToString()));
            }
        }

        foreach (ArchitectureDeclaredTypeFact fact in group.Facts)
        {
            if (!IsNamingSatisfied(fact.SimpleTypeName, contract))
            {
                AddViolation(
                    contract, executionContext, violations,
                    sourceType: fact.FullTypeName,
                    forbiddenReference: $"actual name '{fact.SimpleTypeName}' does not satisfy naming expectation",
                    payload: new LayoutConventionPayload(
                        MatchedFilePath: group.SourceFilePath,
                        ExpectedTypeName: DescribeExpectedName(contract),
                        ActualTypeName: fact.SimpleTypeName));
            }
        }

        if (contract.RequireTypeNameMatchesFileName && group.FileNameWithoutExtension != null
            && !group.Facts.Any(fact => string.Equals(fact.SimpleTypeName, group.FileNameWithoutExtension, StringComparison.Ordinal)))
        {
            string actualNames = string.Join(", ", group.Facts.Select(f => f.SimpleTypeName));
            AddViolation(
                contract, executionContext, violations,
                sourceType: groupLabel,
                forbiddenReference: $"no declared type named '{group.FileNameWithoutExtension}', found: [{actualNames}]",
                payload: new LayoutConventionPayload(
                    MatchedFilePath: group.SourceFilePath,
                    ExpectedTypeName: group.FileNameWithoutExtension,
                    ActualTypeName: actualNames));
        }

        if (contract.RequireMatchingInterface != null)
        {
            EvaluateMatchingInterfaceExpectation(contract, group, executionContext, violations);
        }
    }

    private void EvaluateMatchingInterfaceExpectation(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        string namePrefix = string.IsNullOrEmpty(contract.RequireMatchingInterface!.NamePrefix)
            ? "I"
            : contract.RequireMatchingInterface.NamePrefix!;

        foreach (ArchitectureDeclaredTypeFact fact in group.Facts.Where(f => f.TypeKind == ArchitectureTypeKind.Class))
        {
            string expectedCounterpartName = namePrefix + fact.SimpleTypeName;
            List<ArchitectureDeclaredTypeFact> candidates = SourceFileFactIndex.AllFacts
                .Where(candidate => candidate.TypeKind == ArchitectureTypeKind.Interface
                    && string.Equals(candidate.SimpleTypeName, expectedCounterpartName, StringComparison.Ordinal))
                .ToList();

            if (candidates.Count == 1)
            {
                continue;
            }

            string reason = candidates.Count == 0
                ? $"no matching interface '{expectedCounterpartName}' found"
                : $"ambiguous matching interface '{expectedCounterpartName}': {candidates.Count} candidates found";

            AddViolation(
                contract, executionContext, violations,
                sourceType: fact.FullTypeName,
                forbiddenReference: reason,
                payload: new LayoutConventionPayload(
                    MatchedFilePath: group.SourceFilePath,
                    ExpectedCounterpartName: expectedCounterpartName));
        }
    }

    private static void AddViolation(
        ArchitectureLayoutConventionContract contract,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        string sourceType,
        string forbiddenReference,
        LayoutConventionPayload payload)
    {
        if (executionContext.IsIgnored(sourceType, forbiddenReference))
        {
            return;
        }

        violations.Add(new ArchitectureViolation(
            contract.Name,
            contract.Id,
            sourceType,
            forbiddenReference,
            new[] { forbiddenReference })
        {
            Payload = payload
        });
    }

    private static bool IsNamingSatisfied(string typeName, ArchitectureLayoutConventionContract contract)
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

    private static string DescribeExpectedName(ArchitectureLayoutConventionContract contract)
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

    private static ArchitectureTypeKind ParseTypeKind(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out ArchitectureTypeKind kind)
            ? kind
            : throw new InvalidOperationException(
                $"Unrecognized type kind '{value}'. Expected one of: class, interface, struct, enum, record, delegate.");
    }

    private sealed record LayoutFileGroup(
        string? SourceFilePath,
        string? FileNameWithoutExtension,
        List<ArchitectureDeclaredTypeFact> Facts);
}
