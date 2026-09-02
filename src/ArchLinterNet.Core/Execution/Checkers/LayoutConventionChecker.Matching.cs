using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// Candidate/file-group selection for the layout_conventions family. Split from
// LayoutConventionChecker.cs to keep both files under the repository's file-size lint budget
// (make/lint.mk CS_SIZE_LINT_ERROR_LINES).
internal static partial class LayoutConventionChecker
{
    // A whole-run data-unavailable diagnostic may be caused by an expectation (for example,
    // require_type_name_matches_file_name), while some authored selectors remain evaluable from
    // reflection-only namespace facts. Record every selector in that case: a missing path is
    // explicit EvaluationFailed evidence, and an independent namespace-only result stays useful
    // rather than silently disappearing from coverage/explain.
    private static void RecordUnavailableSelectorParticipation(
        ArchitectureLayoutConventionContract contract, ArchitectureCheckerContext context)
    {
        (_, List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled) = BuildCandidateIndex(context);
        bool inclusionEvaluationFailed = MatcherNeedsSourcePath(contract.FilesMatching);
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> included = inclusionEvaluationFailed
            ? new List<(Type, ArchitectureDeclaredTypeFact)>()
            : unfiled.Where(entry => MatchesUnfiledFact(contract.FilesMatching, entry.Fact)
                && (contract.FilesMatching.CompiledWhen == null
                    || EvaluateLayoutWhen(contract.FilesMatching, context, entry.Type)))
                .ToList();

        context.RecordSubtractiveMatcherParticipation(
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
                && (exclusion.CompiledWhen == null || EvaluateLayoutWhen(exclusion, context, entry.Type)));
            context.RecordSubtractiveMatcherParticipation(
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
    private static List<LayoutFileGroup> CollectMatchedFileGroups(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        LayoutExclusionTracker tracker)
    {
        ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
        (Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile,
            List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled) = BuildCandidateIndex(context);

        List<LayoutFileGroup> groups = ProjectFiledCandidateGroups(
            contract,
            context,
            byFile,
            tracker.Matched,
            out bool filedInclusionMatched);
        List<LayoutFileGroup> unfiledGroups = CollectUnfiledGroups(
            contract, context, matcher, unfiled, executionContext, violations, tracker);
        groups.AddRange(unfiledGroups);
        tracker.InclusionMatched = filedInclusionMatched || tracker.InclusionMatched;
        return groups;
    }

    private static (Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> ByFile,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> Unfiled) BuildCandidateIndex(
            ArchitectureCheckerContext context)
    {
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile = new(StringComparer.Ordinal);
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> unfiled = new();

        foreach (Type type in context.TypeIndex.AllTypes())
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (string.IsNullOrEmpty(fullName)
                || !context.SourceFileFactIndex.TryGetFact(assemblyName, fullName, out ArchitectureDeclaredTypeFact fact))
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

    // This is the authoritative projection for every source-file-backed layout selector. Both the
    // normal layout checker and the opt-in applicability inventory consume it so a selector has
    // exactly one file-level meaning: select the file, refine declarations by `when`, then apply
    // file-level exclusions and their own `when` predicates.
    internal static List<LayoutFileGroup> ProjectFiledCandidateGroups(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> byFile,
        bool[] exclusionMatched,
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

            List<ArchitectureDeclaredTypeFact> eligibleFacts = FilterByWhen(contract.FilesMatching, context, entries);
            if (eligibleFacts.Count == 0)
            {
                continue;
            }

            inclusionMatched = true;

            eligibleFacts = ApplyFiledExclusions(
                context, entries, eligibleFacts, contract.ExcludeFilesMatching, exclusionMatched);
            if (eligibleFacts.Count == 0)
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(filePath, entries[0].Fact.FileNameWithoutExtension, eligibleFacts));
        }

        return groups;
    }

    private static List<ArchitectureDeclaredTypeFact> ApplyFiledExclusions(
        ArchitectureCheckerContext context,
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

            foreach (ArchitectureDeclaredTypeFact fact in FilterByWhen(exclusion, context, entries))
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
    private static List<LayoutFileGroup> CollectUnfiledGroups(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
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
                contract, "files_matching", BuildUnevaluatedLayoutWhenExpressions(contract));
            if (!TryEvaluateUnfiledMatcher(
                    context, matcher, entry, executionContext, violations, filesMatchingContext, out bool included))
            {
                tracker.InclusionEvaluationFailed = true;
                continue;
            }

            if (!included)
            {
                continue;
            }

            tracker.InclusionMatched = true;

            if (IsExcludedUnfiledEntry(contract, context, entry, executionContext, violations, tracker))
            {
                continue;
            }

            groups.Add(new LayoutFileGroup(null, null, new List<ArchitectureDeclaredTypeFact> { entry.Fact }));
        }

        return groups;
    }

    private static bool IsExcludedUnfiledEntry(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
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
            ExpressionParticipation[]? whenExpressions = BuildLayoutWhenExpressions(
                exclusion,
                contract.Name,
                fieldName,
                ExpressionParticipationResult.EvaluationFailed);
            MatcherDiagnosticContext exclusionContext = new(contract, fieldName, whenExpressions);
            if (!TryEvaluateUnfiledMatcher(
                    context, exclusion, entry, executionContext, violations, exclusionContext, out bool excluded))
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

    // Bundles the diagnostic identity of the matcher being evaluated — the contract it belongs to,
    // which field it came from, and the pre-built expression-participation payload for its `when` —
    // so TryEvaluateUnfiledMatcher's signature doesn't have to name each separately.
    private readonly record struct MatcherDiagnosticContext(
        ArchitectureLayoutConventionContract Contract,
        string FieldName,
        ExpressionParticipation[]? WhenExpressions);

    private static bool TryEvaluateUnfiledMatcher(
        ArchitectureCheckerContext context,
        ArchitectureLayoutFileMatcher matcher,
        (Type Type, ArchitectureDeclaredTypeFact Fact) entry,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        MatcherDiagnosticContext diagnosticContext,
        out bool matched)
    {
        ArchitectureLayoutConventionContract contract = diagnosticContext.Contract;
        string fieldName = diagnosticContext.FieldName;
        ExpressionParticipation[]? whenExpressions = diagnosticContext.WhenExpressions;
        matched = false;
        if (matcher.CompiledWhen == null)
        {
            matched = true;
            return true;
        }

        bool whenReferencesSourcePath = ReferencesSourcePathIdentifier(matcher.When);
        bool isAmbiguous = context.SourceFileFactIndex.Ambiguities.Any(ambiguity =>
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

        matched = EvaluateLayoutWhen(matcher, context, entry.Type);
        return true;
    }

    private static List<ArchitectureDeclaredTypeFact> FilterByWhen(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureCheckerContext context,
        List<(Type Type, ArchitectureDeclaredTypeFact Fact)> entries)
    {
        if (matcher.CompiledWhen == null)
        {
            return entries.Select(entry => entry.Fact).ToList();
        }

        return entries.Where(entry => EvaluateLayoutWhen(matcher, context, entry.Type))
            .Select(entry => entry.Fact).ToList();
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

    private static bool EvaluateLayoutWhen(
        ArchitectureLayoutFileMatcher matcher, ArchitectureCheckerContext context, Type type)
    {
        var expressionContext = ArchitectureExpressionContextFactory.CreateSelectorContext(
            context.ExpressionFacts.BuildSubjectFacts(type));
        string description =
            $"Layout convention files_matching at '{matcher.WhenLocation?.YamlPath}' (contract: {matcher.WhenContractName}, " +
            $"when: {matcher.When}) for type '{ArchitectureTypeNames.SafeFullName(type)}'";
        return ArchitectureExpressionFactService.Evaluate(
            matcher.CompiledWhen!, expressionContext, description, matcher.WhenLocation);
    }
}
