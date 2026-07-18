using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // Unconditional bare-word match, deliberately not a "smarter" syntax-aware check - mirrors
    // ExpressionCompilationValidator's DependencyIdentifierPattern and its documented rationale:
    // ArchLinterNet.CEL exposes no public API to introspect which identifiers a compiled predicate
    // references, and two prior attempts at hand-rolled CEL-lexical-grammar-aware string scanning
    // in this codebase each found a real bypass. A `when` referencing subject.sourcePaths or
    // subject.sourceDirectoryPrefixes against an empty-facts run would otherwise silently evaluate
    // to `false` for every candidate (an empty list, not an evaluation error) and produce a clean
    // pass that looks identical to "everything complies".
    [GeneratedRegex(@"\b(sourcePaths|sourceDirectoryPrefixes)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SourcePathIdentifierPattern();

    private static bool ReferencesSourcePathIdentifier(string? when) =>
        !string.IsNullOrEmpty(when) && SourcePathIdentifierPattern().IsMatch(when);

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
        // require_type_name_matches_file_name is included here too: it inherently needs a resolved
        // FileNameWithoutExtension, so a namespace_segment-only contract combined with it would
        // otherwise report zero violations forever once every match becomes an "unfiled" group.
        // Record type-kind expectations are included for the same reason: per source-file-fact-index,
        // reflection alone classifies record types as Class/Struct - "Record" is only ever accurate
        // when Roslyn source enrichment succeeded for that specific declaration. A `when` referencing
        // subject.sourcePaths/subject.sourceDirectoryPrefixes is included too: those lists are empty
        // (not an evaluation error) for a candidate with no resolved source file, so a path-based
        // predicate over an entirely unenriched run would otherwise silently exclude every candidate
        // and look like a clean pass.
        bool needsSourcePath = !string.IsNullOrEmpty(contract.FilesMatching.FolderSegment)
            || !string.IsNullOrEmpty(contract.FilesMatching.FileNameSuffix)
            || !string.IsNullOrEmpty(contract.FilesMatching.FileNamePrefix)
            || contract.RequireTypeNameMatchesFileName
            || IsRecordKind(contract.RequireTypeKind)
            || IsRecordKind(contract.ForbidTypeKind)
            || ReferencesSourcePathIdentifier(contract.FilesMatching.When);

        if (needsSourcePath && SourceFileFactIndex.AllFacts.All(fact => fact.SourceFilePath == null))
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

        AddAmbiguousSourceDeclarationViolations(contract, executionContext, violations);

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private static bool IsRecordKind(string value) =>
        ArchitectureLayoutTypeKindParser.TryParse(value, out ArchitectureTypeKind kind) && kind == ArchitectureTypeKind.Record;

    // A partial-class declaration spread across multiple files gets a null SourceFilePath (see
    // source-file-fact-index's ambiguity model) and is therefore invisible to folder_segment/
    // file_name_* selectors - CollectMatchedFileGroups correctly cannot place it in any file group.
    // Left unaddressed, a violating type could dodge a folder-based rule simply by being declared
    // as a partial class, with zero diagnostic explaining why. Ambiguities do carry every candidate
    // declaration path, so when at least one of them would satisfy the folder/file-name selector
    // (and any populated namespace_segment, checked against the fact's always-reliable reflection
    // namespace), report it as unresolvable instead of silently excluding it.
    //
    // A populated `when` still gets the final say, exactly like CollectMatchedFileGroups gives it
    // for ordinary candidates: if `when` would have excluded this type anyway (e.g. it isn't the
    // role the predicate scopes to), reporting it as unresolvable would be a false positive - a
    // blocking diagnostic for a type the policy was never actually going to flag.
    private void AddAmbiguousSourceDeclarationViolations(
        ArchitectureLayoutConventionContract contract,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
        bool selectorNeedsSourcePath = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);
        if (!selectorNeedsSourcePath || SourceFileFactIndex.Ambiguities.Count == 0)
        {
            return;
        }

        Dictionary<(string AssemblyName, string FullTypeName), Type> typesByIdentity = BuildTypeIdentityLookup();

        foreach (ArchitectureDeclaredTypeSourceAmbiguity ambiguity in SourceFileFactIndex.Ambiguities
                     .OrderBy(a => a.FullTypeName, StringComparer.Ordinal))
        {
            if (!AnyCandidatePathMatchesFileSelector(matcher, ambiguity.SourceFilePaths))
            {
                continue;
            }

            if (!SourceFileFactIndex.TryGetFact(ambiguity.AssemblyName, ambiguity.FullTypeName, out ArchitectureDeclaredTypeFact fact))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
                && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
            {
                continue;
            }

            if (matcher.CompiledWhen != null)
            {
                if (!typesByIdentity.TryGetValue((ambiguity.AssemblyName, ambiguity.FullTypeName), out Type? type)
                    || !EvaluateLayoutWhen(matcher, type))
                {
                    continue;
                }
            }

            AddViolation(
                contract, executionContext, violations,
                sourceType: ambiguity.FullTypeName,
                forbiddenReference: "cannot evaluate: declared across multiple source files " +
                    $"({string.Join(", ", ambiguity.SourceFilePaths)}), so its folder/file-name facts are ambiguous",
                payload: new LayoutConventionPayload(DataUnavailable: true));
        }
    }

    private Dictionary<(string AssemblyName, string FullTypeName), Type> BuildTypeIdentityLookup()
    {
        Dictionary<(string, string), Type> lookup = new();
        foreach (Type type in TypeIndex.AllTypes())
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (!string.IsNullOrEmpty(fullName))
            {
                lookup[(assemblyName, fullName)] = type;
            }
        }

        return lookup;
    }

    private static bool AnyCandidatePathMatchesFileSelector(
        ArchitectureLayoutFileMatcher matcher, IReadOnlyList<string> candidatePaths)
    {
        foreach (string path in candidatePaths)
        {
            string[] folderSegments = GetFolderSegmentsFromPath(path);
            string fileName = GetFileNameWithoutExtensionFromPath(path);

            if (!string.IsNullOrEmpty(matcher.FolderSegment) && !folderSegments.Contains(matcher.FolderSegment, StringComparer.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(matcher.FileNameSuffix) && !fileName.EndsWith(matcher.FileNameSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(matcher.FileNamePrefix) && !fileName.StartsWith(matcher.FileNamePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static string[] GetFolderSegmentsFromPath(string normalizedRelativePath)
    {
        int lastSlash = normalizedRelativePath.LastIndexOf('/');
        return lastSlash <= 0 ? Array.Empty<string>() : normalizedRelativePath[..lastSlash].Split('/');
    }

    private static string GetFileNameWithoutExtensionFromPath(string normalizedRelativePath)
    {
        int lastSlash = normalizedRelativePath.LastIndexOf('/');
        string fileName = lastSlash >= 0 ? normalizedRelativePath[(lastSlash + 1)..] : normalizedRelativePath;
        int dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
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

        // Record classification is only Roslyn-accurate on facts with a resolved SourceFilePath; an
        // unfiled group (no source enrichment, or an ambiguous partial-class declaration) reports
        // Class/Struct from reflection alone even for a genuine record - matching or excluding it by
        // TypeKind == Record would silently pass a violating type or false-flag a compliant one.
        if (!string.IsNullOrEmpty(contract.RequireTypeKind))
        {
            ArchitectureTypeKind requiredKind = ParseTypeKind(contract.RequireTypeKind);
            if (requiredKind == ArchitectureTypeKind.Record && group.SourceFilePath == null)
            {
                AddUnresolvedRecordKindViolation(contract, group, executionContext, violations, "require_type_kind");
            }
            else if (!group.Facts.Any(fact => fact.TypeKind == requiredKind))
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
            if (forbiddenKind == ArchitectureTypeKind.Record && group.SourceFilePath == null)
            {
                AddUnresolvedRecordKindViolation(contract, group, executionContext, violations, "forbid_type_kind");
            }
            else
            {
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

        if (contract.RequireTypeNameMatchesFileName)
        {
            // Defense-in-depth for partial source enrichment: the run-level "unavailable" guard
            // above only fires when NO fact anywhere has a resolved source file. A namespace_segment
            // match can still land an individual group with no resolvable file (group.FileNameWithoutExtension
            // == null) even while other facts in the run do have paths - silently skipping such a group
            // would fail open (a policy that loads and "runs" but can never produce this violation).
            if (group.FileNameWithoutExtension == null)
            {
                AddViolation(
                    contract, executionContext, violations,
                    sourceType: groupLabel,
                    forbiddenReference: "require_type_name_matches_file_name cannot be evaluated: no resolvable source " +
                        "file for this declared type (missing source enrichment or an ambiguous partial-class declaration)",
                    payload: new LayoutConventionPayload(DataUnavailable: true));
            }
            else if (!group.Facts.Any(fact => string.Equals(fact.SimpleTypeName, group.FileNameWithoutExtension, StringComparison.Ordinal)))
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

    private static void AddUnresolvedRecordKindViolation(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        string fieldName)
    {
        string groupLabel = group.SourceFilePath ?? group.Facts[0].FullTypeName;
        AddViolation(
            contract, executionContext, violations,
            sourceType: groupLabel,
            forbiddenReference: $"cannot evaluate {fieldName}: record — record vs class/struct classification requires " +
                "source-enriched facts, unavailable for this declared type (missing source enrichment or an ambiguous " +
                "partial-class declaration)",
            payload: new LayoutConventionPayload(DataUnavailable: true));
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
