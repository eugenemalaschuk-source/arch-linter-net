using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// Resolves the legacy "one primary source file" limitation for rules other than declaration
// budgets. A count-only rule is handled by LayoutConventionDeclarationCountChecker because it can
// evaluate an ambiguous partial type directly rather than reporting it as unavailable.
internal static class LayoutConventionAmbiguousDeclarationChecker
{
    internal sealed record Result(
        List<ArchitectureViolation> Violations,
        bool InclusionMatched,
        bool[] ExclusionMatched);

    internal static Result Check(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        var result = new Result([], InclusionMatched: false, new bool[contract.ExcludeFilesMatching.Count]);
        ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
        bool selectorNeedsSourcePath = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);
        if (!selectorNeedsSourcePath
            || !HasExpectationOtherThanDeclarationCount(contract)
            || context.SourceFileFactIndex.Ambiguities.Count == 0)
        {
            return result;
        }

        foreach (ArchitectureDeclaredTypeSourceAmbiguity ambiguity in context.SourceFileFactIndex.Ambiguities
                     .OrderBy(ambiguity => ambiguity.FullTypeName, StringComparer.Ordinal))
        {
            if (!IsUnresolvableMatch(contract, matcher, context, ambiguity, typesByIdentity))
            {
                continue;
            }

            result = result with { InclusionMatched = true };
            if (MatchesAnyExclusion(contract, context, ambiguity, typesByIdentity, result.ExclusionMatched))
            {
                continue;
            }

            LayoutConventionChecker.AddViolation(
                contract,
                executionContext,
                result.Violations,
                sourceType: ambiguity.FullTypeName,
                forbiddenReference: "cannot evaluate: declared across multiple source files " +
                    $"({string.Join(", ", ambiguity.SourceFilePaths)}), so its folder/file-name facts are ambiguous",
                payload: new LayoutConventionPayload(DataUnavailable: true)
                {
                    WhenExpressions = LayoutConventionChecker.BuildLayoutWhenExpressions(contract),
                });
        }

        return result;
    }

    private static bool IsUnresolvableMatch(
        ArchitectureLayoutConventionContract contract,
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureCheckerContext context,
        ArchitectureDeclaredTypeSourceAmbiguity ambiguity,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        if (!LayoutConventionChecker.AnyCandidatePathMatchesFileSelector(matcher, ambiguity.SourceFilePaths)
            || !context.SourceFileFactIndex.TryGetFact(
                ambiguity.AssemblyName, ambiguity.FullTypeName, out ArchitectureDeclaredTypeFact fact)
            || !CanProduceViolation(contract, fact))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
            && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
        {
            return false;
        }

        return LayoutConventionChecker.MatchesWhenForSourceType(
            matcher, context, ambiguity.AssemblyName, ambiguity.FullTypeName, typesByIdentity);
    }

    private static bool CanProduceViolation(
        ArchitectureLayoutConventionContract contract,
        ArchitectureDeclaredTypeFact fact)
    {
        bool hasExpectationOtherThanForbiddenKind = !string.IsNullOrEmpty(contract.RequireTypeKind)
            || !string.IsNullOrEmpty(contract.RequiredNameSuffix)
            || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
            || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
            || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix)
            || contract.RequireTypeNameMatchesFileName
            || contract.RequireMatchingInterface is not null;
        if (hasExpectationOtherThanForbiddenKind || string.IsNullOrEmpty(contract.ForbidTypeKind))
        {
            return true;
        }

        return ArchitectureLayoutTypeKindParser.TryParse(contract.ForbidTypeKind, out ArchitectureTypeKind forbiddenKind)
            && (forbiddenKind == ArchitectureTypeKind.Record || fact.TypeKind == forbiddenKind);
    }

    private static bool HasExpectationOtherThanDeclarationCount(ArchitectureLayoutConventionContract contract) =>
        !string.IsNullOrEmpty(contract.RequireTypeKind)
        || !string.IsNullOrEmpty(contract.ForbidTypeKind)
        || !string.IsNullOrEmpty(contract.RequiredNameSuffix)
        || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
        || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
        || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix)
        || contract.RequireTypeNameMatchesFileName
        || contract.RequireMatchingInterface is not null;

    private static bool MatchesAnyExclusion(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
        ArchitectureDeclaredTypeSourceAmbiguity ambiguity,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity,
        bool[] exclusionMatched)
    {
        bool excluded = false;
        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            ArchitectureLayoutFileMatcher exclusion = contract.ExcludeFilesMatching[index];
            if (!LayoutConventionChecker.AnyCandidatePathMatchesFileSelector(exclusion, ambiguity.SourceFilePaths)
                || !context.SourceFileFactIndex.TryGetFact(
                    ambiguity.AssemblyName, ambiguity.FullTypeName, out ArchitectureDeclaredTypeFact fact)
                || (!string.IsNullOrEmpty(exclusion.NamespaceSegment)
                    && !fact.NamespaceSegments.Contains(exclusion.NamespaceSegment, StringComparer.Ordinal))
                || !LayoutConventionChecker.MatchesWhenForSourceType(
                    exclusion, context, ambiguity.AssemblyName, ambiguity.FullTypeName, typesByIdentity))
            {
                continue;
            }

            exclusionMatched[index] = true;
            excluded = true;
        }

        return excluded;
    }
}
