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
        bool needsSourcePath = MatcherNeedsSourcePath(contract.FilesMatching)
            || contract.ExcludeFilesMatching.Any(MatcherNeedsSourcePath)
            || contract.RequireTypeNameMatchesFileName
            || IsRecordKind(contract.RequireTypeKind)
            || IsRecordKind(contract.ForbidTypeKind);

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
                {
                    WhenExpressions = BuildUnavailableLayoutWhenExpressions(contract),
                }
            });

            RecordUnavailableLayoutSelectorParticipation(contract);

            return violations;
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        // Built once, shared by every path below that needs to resolve a live Type from a fact's
        // (assembly, full name) identity: the ambiguous-declaration `when` check, the unfiled-fact
        // `when`-on-missing-path check, and require_matching_interface's abstract-class exclusion.
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity =
            contract.FilesMatching.CompiledWhen != null
            || contract.ExcludeFilesMatching.Any(matcher => matcher.CompiledWhen != null)
            || contract.RequireMatchingInterface != null
                ? BuildTypeIdentityLookup()
                : null;

        LayoutExclusionTracker tracker = new(contract.ExcludeFilesMatching.Count);
        List<LayoutFileGroup> matchedGroups = CollectMatchedFileGroups(contract, executionContext, violations, tracker);

        RecordSubtractiveMatcherParticipation(
            contract, "files_matching", null, tracker.InclusionMatched, evaluationFailed: tracker.InclusionEvaluationFailed,
            kind: ArchitectureSelectorParticipationKind.Inclusion);

        foreach (LayoutFileGroup group in matchedGroups)
        {
            EvaluateFileGroupExpectations(contract, group, executionContext, violations, typesByIdentity);
        }

        AddAmbiguousSourceDeclarationViolations(contract, executionContext, violations, typesByIdentity, tracker);

        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            RecordSubtractiveMatcherParticipation(
                contract, "exclude_files_matching", index, tracker.Matched[index],
                evaluationFailed: tracker.EvaluationFailed[index]);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // A files_matching group only exists because its `when` (if any) already evaluated true for
    // every fact in it - see CollectFiledGroups/CollectUnfiledGroups, which filter via
    // EvaluateLayoutWhen before a group is ever constructed - so every violation raised against an
    // already-built group's facts always reports ExpressionParticipationResult.Matched, mirroring
    // ArchitectureAnalysisSession.CheckingContext's AddWhenExpression for the same reason. Returns a
    // list (of at most one entry, since layout conventions have exactly one `when` location) for
    // uniformity with the contextual dependency/allow-only payloads' WhenExpressions shape.
    private static IReadOnlyList<ExpressionParticipation>? BuildLayoutWhenExpressions(
        ArchitectureLayoutConventionContract contract) =>
        BuildLayoutWhenExpressions(
            contract.FilesMatching,
            contract.Name,
            "files_matching",
            ExpressionParticipationResult.Matched);

    private static IReadOnlyList<ExpressionParticipation>? BuildLayoutWhenExpressions(
        ArchitectureLayoutFileMatcher matcher,
        string contractName,
        string fieldName,
        ExpressionParticipationResult result) =>
        matcher.CompiledWhen == null
            ? null
            : new[]
            {
                new ExpressionParticipation(
                    matcher.WhenContractName ?? contractName,
                    fieldName,
                    matcher.When!,
                    matcher.WhenLocation?.YamlPath,
                    result)
                {
                    PolicySourcePath = matcher.WhenLocation?.SourcePath,
                    PolicySourceLine = matcher.WhenLocation?.Line,
                    PolicySourceColumn = matcher.WhenLocation?.Column,
                },
            };

    private static IReadOnlyList<ExpressionParticipation>? BuildUnevaluatedLayoutWhenExpressions(
        ArchitectureLayoutConventionContract contract) =>
        BuildLayoutWhenExpressions(
            contract.FilesMatching,
            contract.Name,
            "files_matching",
            ExpressionParticipationResult.EvaluationFailed);

    private static List<ExpressionParticipation>? BuildUnavailableLayoutWhenExpressions(
        ArchitectureLayoutConventionContract contract)
    {
        List<ExpressionParticipation> expressions = new();
        IReadOnlyList<ExpressionParticipation>? include = BuildUnevaluatedLayoutWhenExpressions(contract);
        if (include != null)
        {
            expressions.AddRange(include);
        }

        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = contract.ExcludeFilesMatching[index];
            if (!MatcherNeedsSourcePath(exclusion) || exclusion.CompiledWhen == null)
            {
                continue;
            }

            IReadOnlyList<ExpressionParticipation>? exclusionExpressions = BuildLayoutWhenExpressions(
                exclusion,
                contract.Name,
                $"exclude_files_matching[{index}]",
                ExpressionParticipationResult.EvaluationFailed);
            if (exclusionExpressions != null)
            {
                expressions.AddRange(exclusionExpressions);
            }
        }

        return expressions.Count == 0 ? null : expressions;
    }

    private static bool MatcherNeedsSourcePath(ArchitectureLayoutFileMatcher matcher) =>
        !string.IsNullOrEmpty(matcher.FolderSegment)
        || !string.IsNullOrEmpty(matcher.FileNameSuffix)
        || !string.IsNullOrEmpty(matcher.FileNamePrefix)
        || ReferencesSourcePathIdentifier(matcher.When);

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
        List<ArchitectureViolation> violations,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity,
        LayoutExclusionTracker tracker)
    {
        ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
        bool selectorNeedsSourcePath = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);
        if (!selectorNeedsSourcePath || SourceFileFactIndex.Ambiguities.Count == 0)
        {
            return;
        }

        foreach (ArchitectureDeclaredTypeSourceAmbiguity ambiguity in SourceFileFactIndex.Ambiguities
                     .OrderBy(a => a.FullTypeName, StringComparer.Ordinal))
        {
            if (!IsUnresolvableAmbiguousMatch(matcher, ambiguity, typesByIdentity))
            {
                continue;
            }

            if (MatchesAnyExclusionForAmbiguity(contract, ambiguity, typesByIdentity, tracker))
            {
                continue;
            }

            AddViolation(
                contract, executionContext, violations,
                sourceType: ambiguity.FullTypeName,
                forbiddenReference: "cannot evaluate: declared across multiple source files " +
                    $"({string.Join(", ", ambiguity.SourceFilePaths)}), so its folder/file-name facts are ambiguous",
                payload: new LayoutConventionPayload(DataUnavailable: true)
                {
                    WhenExpressions = BuildLayoutWhenExpressions(contract),
                });
        }
    }

    private bool IsUnresolvableAmbiguousMatch(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureDeclaredTypeSourceAmbiguity ambiguity,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        if (!AnyCandidatePathMatchesFileSelector(matcher, ambiguity.SourceFilePaths))
        {
            return false;
        }

        if (!SourceFileFactIndex.TryGetFact(ambiguity.AssemblyName, ambiguity.FullTypeName, out ArchitectureDeclaredTypeFact fact))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
            && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
        {
            return false;
        }

        return MatchesWhenForAmbiguity(matcher, ambiguity, typesByIdentity);
    }

    private bool MatchesWhenForAmbiguity(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureDeclaredTypeSourceAmbiguity ambiguity,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        if (matcher.CompiledWhen == null)
        {
            return true;
        }

        return typesByIdentity != null
            && typesByIdentity.TryGetValue((ambiguity.AssemblyName, ambiguity.FullTypeName), out Type? type)
            && EvaluateLayoutWhen(matcher, type);
    }

    private bool MatchesAnyExclusionForAmbiguity(
        ArchitectureLayoutConventionContract contract,
        ArchitectureDeclaredTypeSourceAmbiguity ambiguity,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity,
        LayoutExclusionTracker tracker)
    {
        // Same rationale as IsExcludedUnfiledEntry: evaluate every authored exclusion against this
        // ambiguity independently, not just until the first one matches, so overlapping exclusions
        // each get their own matched record.
        bool excludedAny = false;
        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = contract.ExcludeFilesMatching[index];
            if (!AnyCandidatePathMatchesFileSelector(exclusion, ambiguity.SourceFilePaths))
            {
                continue;
            }

            if (!SourceFileFactIndex.TryGetFact(ambiguity.AssemblyName, ambiguity.FullTypeName, out ArchitectureDeclaredTypeFact fact))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(exclusion.NamespaceSegment)
                && !fact.NamespaceSegments.Contains(exclusion.NamespaceSegment, StringComparer.Ordinal))
            {
                continue;
            }

            if (MatchesWhenForAmbiguity(exclusion, ambiguity, typesByIdentity))
            {
                tracker.Matched[index] = true;
                excludedAny = true;
            }
        }

        return excludedAny;
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

    private void EvaluateFileGroupExpectations(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        EvaluateRequireTypeKind(contract, group, executionContext, violations);
        EvaluateForbidTypeKind(contract, group, executionContext, violations);
        EvaluateNamingExpectations(contract, group, executionContext, violations);
        EvaluateRequireTypeNameMatchesFileName(contract, group, executionContext, violations);

        if (contract.RequireMatchingInterface != null)
        {
            EvaluateMatchingInterfaceExpectation(contract, group, executionContext, violations, typesByIdentity);
        }
    }

    // Record classification is only Roslyn-accurate on facts with a resolved SourceFilePath; an
    // unfiled group (no source enrichment, or an ambiguous partial-class declaration) reports
    // Class/Struct from reflection alone even for a genuine record - matching or excluding it by
    // TypeKind == Record would silently pass a violating type or false-flag a compliant one.
    private static void EvaluateRequireTypeKind(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (string.IsNullOrEmpty(contract.RequireTypeKind))
        {
            return;
        }

        ArchitectureTypeKind requiredKind = ParseTypeKind(contract.RequireTypeKind);
        if (requiredKind == ArchitectureTypeKind.Record && group.SourceFilePath == null)
        {
            AddUnresolvedRecordKindViolation(contract, group, executionContext, violations, "require_type_kind");
            return;
        }

        if (group.Facts.Any(fact => fact.TypeKind == requiredKind))
        {
            return;
        }

        string groupLabel = group.SourceFilePath ?? group.Facts[0].FullTypeName;
        string actualKinds = string.Join(", ", group.Facts.Select(f => f.TypeKind.ToString()).Distinct(StringComparer.Ordinal));
        AddViolation(
            contract, executionContext, violations,
            sourceType: groupLabel,
            forbiddenReference: $"expected type kind '{contract.RequireTypeKind}', found: [{actualKinds}]",
            payload: new LayoutConventionPayload(
                MatchedFilePath: group.SourceFilePath,
                ExpectedTypeKind: contract.RequireTypeKind,
                ActualTypeKind: actualKinds)
            {
                WhenExpressions = BuildLayoutWhenExpressions(contract),
            });
    }

    private static void EvaluateForbidTypeKind(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (string.IsNullOrEmpty(contract.ForbidTypeKind))
        {
            return;
        }

        ArchitectureTypeKind forbiddenKind = ParseTypeKind(contract.ForbidTypeKind);
        if (forbiddenKind == ArchitectureTypeKind.Record && group.SourceFilePath == null)
        {
            AddUnresolvedRecordKindViolation(contract, group, executionContext, violations, "forbid_type_kind");
            return;
        }

        foreach (ArchitectureDeclaredTypeFact fact in group.Facts.Where(f => f.TypeKind == forbiddenKind))
        {
            AddViolation(
                contract, executionContext, violations,
                sourceType: fact.FullTypeName,
                forbiddenReference: $"forbidden type kind '{contract.ForbidTypeKind}'",
                payload: new LayoutConventionPayload(
                    MatchedFilePath: group.SourceFilePath,
                    ExpectedTypeKind: $"not {contract.ForbidTypeKind}",
                    ActualTypeKind: fact.TypeKind.ToString())
                {
                    WhenExpressions = BuildLayoutWhenExpressions(contract),
                });
        }
    }

    private static void EvaluateNamingExpectations(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach (ArchitectureDeclaredTypeFact fact in group.Facts)
        {
            bool namingOk = ArchitectureNameConventionMatcher.Matches(
                fact.SimpleTypeName, contract.RequiredNameSuffix, contract.RequiredNamePrefix,
                contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix);
            if (namingOk)
            {
                continue;
            }

            AddViolation(
                contract, executionContext, violations,
                sourceType: fact.FullTypeName,
                forbiddenReference: $"actual name '{fact.SimpleTypeName}' does not satisfy naming expectation",
                payload: new LayoutConventionPayload(
                    MatchedFilePath: group.SourceFilePath,
                    ExpectedTypeName: ArchitectureNameConventionMatcher.Describe(
                        contract.RequiredNameSuffix, contract.RequiredNamePrefix,
                        contract.ForbiddenNameSuffix, contract.ForbiddenNamePrefix),
                    ActualTypeName: fact.SimpleTypeName)
                {
                    WhenExpressions = BuildLayoutWhenExpressions(contract),
                });
        }
    }

    // Defense-in-depth for partial source enrichment: the run-level "unavailable" guard fires only
    // when NO fact anywhere has a resolved source file. A namespace_segment match can still land an
    // individual group with no resolvable file (group.FileNameWithoutExtension == null) even while
    // other facts in the run do have paths - silently skipping such a group would fail open (a
    // policy that loads and "runs" but can never produce this violation).
    private static void EvaluateRequireTypeNameMatchesFileName(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (!contract.RequireTypeNameMatchesFileName)
        {
            return;
        }

        string groupLabel = group.SourceFilePath ?? group.Facts[0].FullTypeName;

        if (group.FileNameWithoutExtension == null)
        {
            AddViolation(
                contract, executionContext, violations,
                sourceType: groupLabel,
                forbiddenReference: "require_type_name_matches_file_name cannot be evaluated: no resolvable source " +
                    "file for this declared type (missing source enrichment or an ambiguous partial-class declaration)",
                payload: new LayoutConventionPayload(DataUnavailable: true)
                {
                    WhenExpressions = BuildLayoutWhenExpressions(contract),
                });
            return;
        }

        if (group.Facts.Any(fact => string.Equals(fact.SimpleTypeName, group.FileNameWithoutExtension, StringComparison.Ordinal)))
        {
            return;
        }

        string actualNames = string.Join(", ", group.Facts.Select(f => f.SimpleTypeName));
        AddViolation(
            contract, executionContext, violations,
            sourceType: groupLabel,
            forbiddenReference: $"no declared type named '{group.FileNameWithoutExtension}', found: [{actualNames}]",
            payload: new LayoutConventionPayload(
                MatchedFilePath: group.SourceFilePath,
                ExpectedTypeName: group.FileNameWithoutExtension,
                ActualTypeName: actualNames)
            {
                WhenExpressions = BuildLayoutWhenExpressions(contract),
            });
    }

    private void EvaluateMatchingInterfaceExpectation(
        ArchitectureLayoutConventionContract contract,
        LayoutFileGroup group,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        string namePrefix = string.IsNullOrEmpty(contract.RequireMatchingInterface!.NamePrefix)
            ? "I"
            : contract.RequireMatchingInterface.NamePrefix!;

        // A matching-interface counterpart is only meaningful for a concrete class: an abstract
        // class is itself an extension point (a base for concrete implementations to satisfy),
        // not a leaf type callers depend on through an interface seam, so requiring an I-prefixed
        // interface for it would be a spurious violation. ArchitectureDeclaredTypeFact carries no
        // IsAbstract field (source-file-fact-index's fact model is CLR-kind-only), so this resolves
        // the live reflected Type via the shared identity lookup to read it.
        foreach (ArchitectureDeclaredTypeFact fact in group.Facts.Where(f => f.TypeKind == ArchitectureTypeKind.Class))
        {
            if (typesByIdentity != null
                && typesByIdentity.TryGetValue((fact.AssemblyName, fact.FullTypeName), out Type? type)
                && type.IsAbstract)
            {
                continue;
            }

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
                    ExpectedCounterpartName: expectedCounterpartName)
                {
                    WhenExpressions = BuildLayoutWhenExpressions(contract),
                });
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
            payload: new LayoutConventionPayload(DataUnavailable: true)
            {
                WhenExpressions = BuildLayoutWhenExpressions(contract),
            });
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
