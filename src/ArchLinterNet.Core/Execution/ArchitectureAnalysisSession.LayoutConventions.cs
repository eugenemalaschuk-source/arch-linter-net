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

        // Only folder/file-name selector fields require source-enriched facts; namespace_segment works
        // from reflection-derived namespace facts alone. A contract using namespace_segment only must
        // keep evaluating even when no source enrichment happened for this run - disabling it
        // unconditionally would silently turn a working namespace-only rule into a permanent no-op.
        bool selectorNeedsSourcePath = !string.IsNullOrEmpty(contract.FilesMatching.FolderSegment)
            || !string.IsNullOrEmpty(contract.FilesMatching.FileNameSuffix)
            || !string.IsNullOrEmpty(contract.FilesMatching.FileNamePrefix);

        if (selectorNeedsSourcePath && SourceFileFactIndex.AllFacts.All(fact => fact.SourceFilePath == null))
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

    // File selection is file-granular, not fact-granular: a file matches folder_segment/file_name_*
    // (shared by every type declared in it) or namespace_segment (true if ANY declared type in the
    // file has that namespace segment) as a whole, and once a file matches, every declared type in
    // it becomes a candidate - not just the one(s) whose own namespace happened to match. Matching
    // fact-by-fact instead would let an offending type escape every expectation just by being
    // declared under a different namespace in the same already-selected file. Facts with no
    // resolvable source file (no source enrichment, or an ambiguous partial-class declaration) can
    // only ever satisfy namespace_segment, evaluated per-type since there is no file to group by.
    private List<LayoutFileGroup> CollectMatchedFileGroups(ArchitectureLayoutFileMatcher matcher)
    {
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile = new(StringComparer.Ordinal);
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled = new();

        foreach (Type type in TypeIndex.AllTypes())
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (string.IsNullOrEmpty(fullName)
                || !SourceFileFactIndex.TryGetFact(assemblyName, fullName, out ArchitectureDeclaredTypeFact fact))
            {
                continue;
            }

            if (fact.SourceFilePath != null)
            {
                if (!byFile.TryGetValue(fact.SourceFilePath, out List<(Type Type, ArchitectureDeclaredTypeFact Fact)>? entries))
                {
                    entries = new List<(Type, ArchitectureDeclaredTypeFact)>();
                    byFile[fact.SourceFilePath] = entries;
                }

                entries.Add((type, fact));
            }
            else
            {
                unfiled.Add((type, fact));
            }
        }

        List<LayoutFileGroup> groups = new();

        foreach ((string filePath, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries) in
                 byFile.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!MatchesFileLevelSelector(matcher, entries))
            {
                continue;
            }

            List<ArchitectureDeclaredTypeFact> eligibleFacts = FilterByWhen(matcher, entries);
            if (eligibleFacts.Count == 0)
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(filePath, entries[0].Fact.FileNameWithoutExtension, eligibleFacts));
        }

        foreach ((Type Type, ArchitectureDeclaredTypeFact Fact) entry in
                 unfiled.OrderBy(entry => entry.Fact.FullTypeName, StringComparer.Ordinal))
        {
            if (!MatchesUnfiledFact(matcher, entry.Fact))
            {
                continue;
            }

            if (matcher.CompiledWhen != null && !EvaluateLayoutWhen(matcher, entry.Type))
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(null, null, new List<ArchitectureDeclaredTypeFact> { entry.Fact }));
        }

        return groups;
    }

    private List<ArchitectureDeclaredTypeFact> FilterByWhen(
        ArchitectureLayoutFileMatcher matcher, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries)
    {
        if (matcher.CompiledWhen == null)
        {
            return entries.Select(entry => entry.Fact).ToList();
        }

        return entries.Where(entry => EvaluateLayoutWhen(matcher, entry.Type)).Select(entry => entry.Fact).ToList();
    }

    private static bool MatchesFileLevelSelector(
        ArchitectureLayoutFileMatcher matcher, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries)
    {
        // Every entry in this list shares the same SourceFilePath, so FolderSegments/FileNameWithoutExtension
        // are identical across all of them - the first entry's fact is representative for those fields.
        ArchitectureDeclaredTypeFact representative = entries[0].Fact;

        if (!string.IsNullOrEmpty(matcher.FolderSegment)
            && !representative.FolderSegments.Contains(matcher.FolderSegment, StringComparer.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.FileNameSuffix)
            && (representative.FileNameWithoutExtension == null
                || !representative.FileNameWithoutExtension.EndsWith(matcher.FileNameSuffix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.FileNamePrefix)
            && (representative.FileNameWithoutExtension == null
                || !representative.FileNameWithoutExtension.StartsWith(matcher.FileNamePrefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
            && !entries.Any(entry => entry.Fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesUnfiledFact(ArchitectureLayoutFileMatcher matcher, ArchitectureDeclaredTypeFact fact)
    {
        bool requiresSourceFile = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);
        if (requiresSourceFile)
        {
            return false;
        }

        // LayoutConventionsValidator guarantees at least one files_matching field is populated; with
        // requiresSourceFile false, namespace_segment must be the populated one.
        return fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal);
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
            bool namingOk = ArchitectureNameConventionMatcher.Matches(
                fact.SimpleTypeName, contract.RequiredNameSuffix, contract.RequiredNamePrefix,
                contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix);
            if (!namingOk)
            {
                AddViolation(
                    contract, executionContext, violations,
                    sourceType: fact.FullTypeName,
                    forbiddenReference: $"actual name '{fact.SimpleTypeName}' does not satisfy naming expectation",
                    payload: new LayoutConventionPayload(
                        MatchedFilePath: group.SourceFilePath,
                        ExpectedTypeName: ArchitectureNameConventionMatcher.Describe(
                            contract.RequiredNameSuffix, contract.RequiredNamePrefix,
                            contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix),
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

    private static ArchitectureTypeKind ParseTypeKind(string value)
    {
        return ArchitectureLayoutTypeKindParser.TryParse(value, out ArchitectureTypeKind kind)
            ? kind
            : throw new InvalidOperationException(
                $"Unrecognized type kind '{value}'. Expected one of: class, interface, struct, enum, record, delegate.");
    }

    private sealed record LayoutFileGroup(
        string? SourceFilePath,
        string? FileNameWithoutExtension,
        List<ArchitectureDeclaredTypeFact> Facts);
}
