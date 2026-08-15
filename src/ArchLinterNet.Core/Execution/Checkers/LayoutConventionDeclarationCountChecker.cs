using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// Evaluates the source-declaration budget as a separate collaborator. A type can have several
// declarations while still being one reflected fact, so this deliberately consumes the complete
// source declaration inventory rather than the fact index's single-path view.
internal static class LayoutConventionDeclarationCountChecker
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
        if (contract.MaxDeclarationsPerType is not int maximum)
        {
            return result;
        }

        foreach (IGrouping<(string AssemblyName, string FullTypeName), ArchitectureTypeSourceDeclaration> declarationGroup
                 in context.SourceFileFactIndex.SourceDeclarations
                     .GroupBy(declaration => (declaration.AssemblyName, declaration.FullTypeName))
                     .OrderBy(group => group.Key.FullTypeName, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.AssemblyName, StringComparer.Ordinal))
        {
            ArchitectureTypeSourceDeclaration[] declarations = declarationGroup
                .OrderBy(declaration => declaration.SourceFilePath, StringComparer.Ordinal)
                .ThenBy(declaration => declaration.SourceLine)
                .ToArray();
            if (declarations.Length <= maximum
                || !context.SourceFileFactIndex.TryGetFact(
                    declarationGroup.Key.AssemblyName, declarationGroup.Key.FullTypeName,
                    out ArchitectureDeclaredTypeFact fact)
                || !MatchesSelector(contract.FilesMatching, context, fact, declarations, typesByIdentity))
            {
                continue;
            }

            result = result with { InclusionMatched = true };
            if (MatchesAnyExclusion(contract, context, fact, declarations, typesByIdentity, result.ExclusionMatched))
            {
                continue;
            }

            string[] paths = declarations
                .Select(declaration => declaration.SourceFilePath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            LayoutConventionChecker.AddViolation(
                contract,
                executionContext,
                result.Violations,
                sourceType: fact.FullTypeName,
                forbiddenReference: $"expected at most {maximum} source declaration(s), found {declarations.Length}: " +
                    string.Join(", ", paths),
                payload: new LayoutConventionPayload(MatchedFilePath: paths[0])
                {
                    ExpectedDeclarationCount = maximum,
                    ActualDeclarationCount = declarations.Length,
                    DeclarationPaths = paths,
                    WhenExpressions = LayoutConventionChecker.BuildLayoutWhenExpressions(contract),
                });
        }

        return result;
    }

    private static bool MatchesSelector(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureCheckerContext context,
        ArchitectureDeclaredTypeFact fact,
        IReadOnlyList<ArchitectureTypeSourceDeclaration> declarations,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        string[] paths = declarations.Select(declaration => declaration.SourceFilePath).ToArray();
        if (!LayoutConventionChecker.AnyCandidatePathMatchesFileSelector(matcher, paths))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamespaceSegment)
            && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
        {
            return false;
        }

        return LayoutConventionChecker.MatchesWhenForSourceType(
            matcher, context, fact.AssemblyName, fact.FullTypeName, typesByIdentity);
    }

    private static bool MatchesAnyExclusion(
        ArchitectureLayoutConventionContract contract,
        ArchitectureCheckerContext context,
        ArchitectureDeclaredTypeFact fact,
        IReadOnlyList<ArchitectureTypeSourceDeclaration> declarations,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity,
        bool[] exclusionMatched)
    {
        bool excluded = false;
        for (int index = 0; index < contract.ExcludeFilesMatching.Count; index++)
        {
            if (!MatchesSelector(
                    contract.ExcludeFilesMatching[index], context, fact, declarations, typesByIdentity))
            {
                continue;
            }

            exclusionMatched[index] = true;
            excluded = true;
        }

        return excluded;
    }
}
