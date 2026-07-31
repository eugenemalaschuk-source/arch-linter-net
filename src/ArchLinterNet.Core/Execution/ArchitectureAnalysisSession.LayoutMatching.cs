using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // A whole-run data-unavailable diagnostic may be caused by an expectation (for example,
    // require_type_name_matches_file_name), while some authored selectors remain evaluable from
    // reflection-only namespace facts. Record every selector in that case: a missing path is
    // explicit EvaluationFailed evidence, and an independent namespace-only result stays useful
    // rather than silently disappearing from coverage/explain.
    private void RecordUnavailableLayoutSelectorParticipation(ArchitectureLayoutConventionContract contract)
    {
        (_, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled) = BuildCandidateIndex();
        bool inclusionEvaluationFailed = MatcherNeedsSourcePath(contract.FilesMatching);
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> included = inclusionEvaluationFailed
            ? new List<(Type, ArchitectureDeclaredTypeFact)>()
            : unfiled.Where(entry => MatchesUnfiledFact(contract.FilesMatching, entry.Fact)
                && (contract.FilesMatching.CompiledWhen == null || EvaluateLayoutWhen(contract.FilesMatching, entry.Type)))
                .ToList();

        RecordSubtractiveMatcherParticipation(
            contract, "files_matching", null, included.Count > 0,
            evaluationFailed: inclusionEvaluationFailed,
            kind: ArchitectureSelectorParticipationKind.Inclusion);

        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = contract.ExcludeFilesMatching[index];
            // An exclusion cannot establish an effective subtraction while its positive universe
            // is unknown, even if the exclusion itself is namespace-only and otherwise evaluable.
            bool evaluationFailed = inclusionEvaluationFailed || MatcherNeedsSourcePath(exclusion);
            bool matched = !evaluationFailed && included.Any(entry =>
                MatchesUnfiledFact(exclusion, entry.Fact)
                && (exclusion.CompiledWhen == null || EvaluateLayoutWhen(exclusion, entry.Type)));
            RecordSubtractiveMatcherParticipation(
                contract, "exclude_files_matching", index, matched, evaluationFailed);
        }
    }

    // File selection is file-granular, not fact-granular: a file matches folder_segment/file_name_*
    // (shared by every type declared in it) or namespace_segment (true if ANY declared type in the
    // file has that namespace segment) as a whole, and once a file matches, every declared type in
    // it becomes a candidate - not just the one(s) whose own namespace happened to match. Matching
    // fact-by-fact instead would let an offending type escape every expectation just by being
    // declared under a different namespace in the same already-selected file. Facts with no
    // resolvable source file (no source enrichment, or an ambiguous partial-class declaration) can
    // only ever satisfy namespace_segment, evaluated per-type since there is no file to group by.
    private List<LayoutFileGroup> CollectMatchedFileGroups(
        ArchitectureLayoutConventionContract contract,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        LayoutExclusionTracker tracker)
    {
        ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
        (Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile,
            List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled) = BuildCandidateIndex();

        List<LayoutFileGroup> groups = CollectFiledGroups(contract, byFile, tracker, out bool filedInclusionMatched);
        List<LayoutFileGroup> unfiledGroups = CollectUnfiledGroups(contract, matcher, unfiled, executionContext, violations, tracker);
        groups.AddRange(unfiledGroups);
        tracker.InclusionMatched = filedInclusionMatched || tracker.InclusionMatched;
        return groups;
    }

    private (Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> ByFile,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> Unfiled) BuildCandidateIndex()
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

            if (fact.SourceFilePath == null)
            {
                unfiled.Add((type, fact));
                continue;
            }

            if (!byFile.TryGetValue(fact.SourceFilePath, out List<(Type Type, ArchitectureDeclaredTypeFact Fact)>? entries))
            {
                entries = new List<(Type, ArchitectureDeclaredTypeFact)>();
                byFile[fact.SourceFilePath] = entries;
            }

            entries.Add((type, fact));
        }

        return (byFile, unfiled);
    }

    private List<LayoutFileGroup> CollectFiledGroups(
        ArchitectureLayoutConventionContract contract,
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile,
        LayoutExclusionTracker tracker,
        out bool inclusionMatched)
    {
        List<LayoutFileGroup> groups = new();
        inclusionMatched = false;

        foreach ((string filePath, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries) in
                 byFile.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!MatchesFileLevelSelector(contract.FilesMatching, entries))
            {
                continue;
            }

            List<ArchitectureDeclaredTypeFact> eligibleFacts = FilterByWhen(contract.FilesMatching, entries);
            if (eligibleFacts.Count == 0)
            {
                continue;
            }

            inclusionMatched = true;

            eligibleFacts = ApplyFiledExclusions(entries, eligibleFacts, contract.ExcludeFilesMatching, tracker.Matched);
            if (eligibleFacts.Count == 0)
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(filePath, entries[0].Fact.FileNameWithoutExtension, eligibleFacts));
        }

        return groups;
    }

    private List<ArchitectureDeclaredTypeFact> ApplyFiledExclusions(
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries,
        List<ArchitectureDeclaredTypeFact> eligibleFacts,
        List<ArchitectureLayoutFileMatcher> exclusions,
        bool[] exclusionMatched)
    {
        if (exclusions.Count == 0)
        {
            return eligibleFacts;
        }

        // An exclusion only "matches" for participation purposes when it actually subtracts a
        // candidate the inclusion selector already accepted - checking against every fact in the
        // file (not just eligibleFacts) would report Matched for a type the include selector's own
        // `when` had already dropped, even though the exclusion removed nothing from this run.
        HashSet<(string AssemblyName, string FullTypeName)> eligibleKeys = eligibleFacts
            .Select(fact => (fact.AssemblyName, fact.FullTypeName))
            .ToHashSet();

        HashSet<(string AssemblyName, string FullTypeName)> excluded = new();
        for (int index = 0; index < exclusions.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = exclusions[index];
            if (!MatchesFileLevelSelector(exclusion, entries))
            {
                continue;
            }

            foreach (ArchitectureDeclaredTypeFact fact in FilterByWhen(exclusion, entries))
            {
                (string AssemblyName, string FullTypeName) key = (fact.AssemblyName, fact.FullTypeName);
                if (!eligibleKeys.Contains(key))
                {
                    continue;
                }

                excluded.Add(key);
                exclusionMatched[index] = true;
            }
        }

        return excluded.Count == 0
            ? eligibleFacts
            : eligibleFacts
                .Where(fact => !excluded.Contains((fact.AssemblyName, fact.FullTypeName)))
                .ToList();
    }

    // A `when` referencing subject.sourcePaths/sourceDirectoryPrefixes evaluates those as an
    // empty list - not an evaluation error - for a fact with no resolved source file, so it can
    // silently include or exclude a candidate the run-level guard never sees (that guard only fires
    // when NO fact anywhere has a path; this is the partial-enrichment case where other facts do).
    // An ambiguous partial-class declaration is exempt: its sourcePaths carries every candidate
    // declaration path (see ArchitectureExpressionSubjectFactBuilder.ResolveSourcePaths), so a
    // path-referencing predicate evaluates against real data for it, same as any filed fact.
    private List<LayoutFileGroup> CollectUnfiledGroups(
        ArchitectureLayoutConventionContract contract,
        ArchitectureLayoutFileMatcher matcher,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        LayoutExclusionTracker tracker)
    {
        List<LayoutFileGroup> groups = new();

        foreach ((Type Type, ArchitectureDeclaredTypeFact Fact) entry in
                 unfiled.OrderBy(entry => entry.Fact.FullTypeName, StringComparer.Ordinal))
        {
            if (!MatchesUnfiledFact(matcher, entry.Fact))
            {
                continue;
            }

            MatcherDiagnosticContext filesMatchingContext = new(
                "files_matching", BuildUnevaluatedLayoutWhenExpressions(contract));
            if (!TryEvaluateUnfiledMatcher(contract, matcher, entry, executionContext, violations, filesMatchingContext, out bool included))
            {
                tracker.InclusionEvaluationFailed = true;
                continue;
            }

            if (!included)
            {
                continue;
            }

            tracker.InclusionMatched = true;

            if (IsExcludedUnfiledEntry(contract, entry, executionContext, violations, tracker))
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(null, null, new List<ArchitectureDeclaredTypeFact> { entry.Fact }));
        }

        return groups;
    }

    private bool IsExcludedUnfiledEntry(
        ArchitectureLayoutConventionContract contract,
        (Type Type, ArchitectureDeclaredTypeFact Fact) entry,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        LayoutExclusionTracker tracker)
    {
        // Every authored exclusion is evaluated against this candidate independently - not just
        // until the first one excludes it - so two overlapping exclusion items both get their own
        // matched/evaluation-failed record instead of the later ones being starved of a chance to
        // ever observe this candidate and misreporting as stale.
        bool excludedAny = false;
        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = contract.ExcludeFilesMatching[index];
            if (!MatchesUnfiledFact(exclusion, entry.Fact))
            {
                continue;
            }

            string fieldName = $"exclude_files_matching[{index}]";
            IReadOnlyList<ExpressionParticipation>? whenExpressions = BuildLayoutWhenExpressions(
                exclusion,
                contract.Name,
                fieldName,
                ExpressionParticipationResult.EvaluationFailed);
            MatcherDiagnosticContext exclusionContext = new(fieldName, whenExpressions);
            if (!TryEvaluateUnfiledMatcher(contract, exclusion, entry, executionContext, violations, exclusionContext, out bool excluded))
            {
                // The exclusion structurally matched this candidate but its `when` couldn't be
                // evaluated (no resolved source file) - this is neither "matched" nor "stale"; the
                // candidate is suppressed defensively (fail-closed, matching TryEvaluateUnfiledMatcher's
                // own DataUnavailable violation) but the matcher's participation status is unknown.
                tracker.EvaluationFailed[index] = true;
                excludedAny = true;
                continue;
            }

            if (excluded)
            {
                tracker.Matched[index] = true;
                excludedAny = true;
            }
        }

        return excludedAny;
    }

    // Bundles the diagnostic identity of the matcher being evaluated (which field it came from, and
    // the pre-built expression-participation payload for its `when`) so TryEvaluateUnfiledMatcher's
    // signature doesn't have to name each separately.
    private readonly record struct MatcherDiagnosticContext(
        string FieldName, IReadOnlyList<ExpressionParticipation>? WhenExpressions);

    private bool TryEvaluateUnfiledMatcher(
        ArchitectureLayoutConventionContract contract,
        ArchitectureLayoutFileMatcher matcher,
        (Type Type, ArchitectureDeclaredTypeFact Fact) entry,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        MatcherDiagnosticContext diagnosticContext,
        out bool matched)
    {
        string fieldName = diagnosticContext.FieldName;
        IReadOnlyList<ExpressionParticipation>? whenExpressions = diagnosticContext.WhenExpressions;
        matched = false;
        if (matcher.CompiledWhen == null)
        {
            matched = true;
            return true;
        }

        bool whenReferencesSourcePath = ReferencesSourcePathIdentifier(matcher.When);
        bool isAmbiguous = SourceFileFactIndex.Ambiguities.Any(ambiguity =>
            ambiguity.AssemblyName == entry.Fact.AssemblyName
            && ambiguity.FullTypeName == entry.Fact.FullTypeName);
        if (whenReferencesSourcePath && !isAmbiguous)
        {
            AddViolation(
                contract,
                executionContext,
                violations,
                sourceType: entry.Fact.FullTypeName,
                forbiddenReference: $"cannot evaluate {fieldName}.when: it references source-path facts " +
                    "(sourcePaths/sourceDirectoryPrefixes), but this declared type has no resolved source file",
                payload: new LayoutConventionPayload(DataUnavailable: true)
                {
                    WhenExpressions = whenExpressions,
                });
            return false;
        }

        matched = EvaluateLayoutWhen(matcher, entry.Type);
        return true;
    }

    private List<ArchitectureDeclaredTypeFact> FilterByWhen(
        ArchitectureLayoutFileMatcher matcher,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries)
    {
        if (matcher.CompiledWhen == null)
        {
            return entries.Select(entry => entry.Fact).ToList();
        }

        return entries.Where(entry => EvaluateLayoutWhen(matcher, entry.Type)).Select(entry => entry.Fact).ToList();
    }

    private static bool MatchesFileLevelSelector(
        ArchitectureLayoutFileMatcher matcher,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries)
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

        return string.IsNullOrEmpty(matcher.NamespaceSegment)
            || fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal);
    }

    private bool EvaluateLayoutWhen(ArchitectureLayoutFileMatcher matcher, Type type)
    {
        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(ExpressionFacts.BuildSubjectFacts(type));
        string description =
            $"Layout convention files_matching at '{matcher.WhenLocation?.YamlPath}' (contract: {matcher.WhenContractName}, " +
            $"when: {matcher.When}) for type '{ArchitectureTypeNames.SafeFullName(type)}'";
        return ArchitectureExpressionFactService.Evaluate(matcher.CompiledWhen!, context, description, matcher.WhenLocation);
    }
}
