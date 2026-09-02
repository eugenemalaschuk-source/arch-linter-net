using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution.Checkers;

/// <summary>
/// Evaluates only the explicit, source-fact-bounded inventory that authors opt into. It neither
/// walks the repository nor attempts to infer a renamed folder from similar text.
/// </summary>
internal static class LayoutConventionApplicabilityChecker
{
    internal const string Family = "layout_convention_applicability";

    internal static Result Evaluate(
        ArchitectureCheckerContext context,
        ArchitectureLayoutConventionApplicabilityContract inventory,
        IReadOnlyList<ArchitectureLayoutConventionContract> conventions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(conventions);

        string inventoryIdentity = inventory.Id ?? inventory.Name;
        ArchitectureLayoutConventionContract[] linkedConventions = inventory.ExpectedFolders
            .Select(folder => conventions.Single(convention => string.Equals(
                convention.Id, folder.ConventionId, StringComparison.Ordinal)))
            .ToArray();
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity = linkedConventions
            .Any(ConventionUsesWhen)
                ? LayoutConventionChecker.BuildTypeIdentityLookup(context)
                : null;
        SourceSubject[] subjects = BuildSubjects(context, inventory.Scope);
        var expectedEntries = new List<ArchitectureApplicabilityExpectedEntry>();
        var records = new List<ArchitectureApplicabilityRecord>();

        for (int index = 0; index < inventory.ExpectedFolders.Count; index++)
        {
            ArchitectureLayoutConventionExpectedFolder expected = inventory.ExpectedFolders[index];
            string controlIdentity = $"{inventoryIdentity}/{expected.Id}:{expected.ConventionId}";
            ArchitectureApplicabilityProvenance provenance = new(Family, controlIdentity, inventoryIdentity);
            string expectedPath = Combine(inventory.Scope, expected.Path);
            SourceSubject[] folderSubjects = subjects
                .Where(subject => IsSameOrDescendant(subject.DirectoryPath, expectedPath))
                .ToArray();
            expectedEntries.Add(new ArchitectureApplicabilityExpectedEntry(
                controlIdentity,
                Family,
                ArchitectureApplicabilityMembership.Required,
                provenance));
            if (folderSubjects.Length == 0)
            {
                records.Add(Unassessable(
                    controlIdentity,
                    ArchitectureApplicabilityReasonCodes.StaleDeclaration,
                    provenance));
                continue;
            }

            records.Add(folderSubjects.Any(subject => MatchesConvention(
                    linkedConventions[index], subject.Fact, context, typesByIdentity))
                ? Evaluable(controlIdentity, provenance)
                : Unassessable(
                    controlIdentity,
                    ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput,
                    provenance));
        }

        if (inventory.Exhaustive)
        {
            string controlIdentity = $"{inventoryIdentity}/scope";
            ArchitectureApplicabilityProvenance provenance = new(Family, controlIdentity, inventoryIdentity);
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceSubject subject in subjects)
            {
                int distinctConventionMappings = Enumerable.Range(0, inventory.ExpectedFolders.Count)
                    .Where(index => IsSameOrDescendant(
                        subject.DirectoryPath,
                        Combine(inventory.Scope, inventory.ExpectedFolders[index].Path))
                        && MatchesConvention(linkedConventions[index], subject.Fact, context, typesByIdentity))
                    .Select(index => linkedConventions[index].Id)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (distinctConventionMappings == 0)
                {
                    reasons.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
                }
                else if (distinctConventionMappings > 1)
                {
                    reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
                }
            }

            expectedEntries.Add(new ArchitectureApplicabilityExpectedEntry(
                controlIdentity,
                Family,
                ArchitectureApplicabilityMembership.Required,
                provenance));
            records.Add(reasons.Count == 0
                ? Evaluable(controlIdentity, provenance)
                : new ArchitectureApplicabilityRecord(
                    controlIdentity,
                    Family,
                    ArchitectureApplicabilityRecordState.Unassessable,
                    reasons.Order(StringComparer.Ordinal)
                        .Select(code => new ArchitectureApplicabilityReason(code, provenance))
                        .ToArray(),
                    provenance));
        }

        return new Result(expectedEntries, records);
    }

    private static SourceSubject[] BuildSubjects(
        ArchitectureCheckerContext context,
        string scope)
    {
        string normalizedScope = Normalize(scope);
        return context.SourceFileFactIndex.SourceDeclarations
            .GroupBy(declaration => (
                declaration.AssemblyName,
                declaration.FullTypeName,
                declaration.SourceFilePath))
            .Select(group => group.First())
            .Select(declaration => context.SourceFileFactIndex.TryGetFact(
                declaration.AssemblyName,
                declaration.FullTypeName,
                out ArchitectureDeclaredTypeFact fact)
                    ? CreateSourceSubject(fact, declaration.SourceFilePath)
                    : null)
            .OfType<SourceSubject>()
            .Where(subject => IsSameOrDescendant(subject.DirectoryPath, normalizedScope))
            .OrderBy(subject => subject.DirectoryPath, StringComparer.Ordinal)
            .ThenBy(subject => subject.Fact.FullTypeName, StringComparer.Ordinal)
            .ThenBy(subject => subject.Fact.AssemblyName, StringComparer.Ordinal)
            .ThenBy(subject => subject.Fact.SourceFilePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool MatchesConvention(
        ArchitectureLayoutConventionContract convention,
        ArchitectureDeclaredTypeFact fact,
        ArchitectureCheckerContext context,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        if (!MatchesMatcher(convention.FilesMatching, fact, context, typesByIdentity))
        {
            return false;
        }

        return !convention.ExcludeFilesMatching.Any(matcher =>
            MatchesMatcher(matcher, fact, context, typesByIdentity));
    }

    private static bool MatchesMatcher(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureDeclaredTypeFact fact,
        ArchitectureCheckerContext context,
        Dictionary<(string AssemblyName, string FullTypeName), Type>? typesByIdentity)
    {
        if (fact.SourceFilePath is null
            || (!string.IsNullOrEmpty(matcher.FolderSegment)
                && !fact.FolderSegments.Contains(matcher.FolderSegment, StringComparer.Ordinal))
            || (!string.IsNullOrEmpty(matcher.NamespaceSegment)
                && !fact.NamespaceSegments.Contains(matcher.NamespaceSegment, StringComparer.Ordinal))
            || (!string.IsNullOrEmpty(matcher.FileNameSuffix)
                && (fact.FileNameWithoutExtension is null
                    || !fact.FileNameWithoutExtension.EndsWith(matcher.FileNameSuffix, StringComparison.Ordinal)))
            || (!string.IsNullOrEmpty(matcher.FileNamePrefix)
                && (fact.FileNameWithoutExtension is null
                    || !fact.FileNameWithoutExtension.StartsWith(matcher.FileNamePrefix, StringComparison.Ordinal))))
        {
            return false;
        }

        return LayoutConventionChecker.MatchesWhenForSourceType(
            matcher,
            context,
            fact.AssemblyName,
            fact.FullTypeName,
            typesByIdentity);
    }

    private static bool ConventionUsesWhen(ArchitectureLayoutConventionContract convention) =>
        convention.FilesMatching.CompiledWhen is not null
        || convention.ExcludeFilesMatching.Any(matcher => matcher.CompiledWhen is not null);

    private static SourceSubject CreateSourceSubject(
        ArchitectureDeclaredTypeFact fact,
        string sourceFilePath)
    {
        string normalizedPath = Normalize(sourceFilePath);
        string directoryPath = DirectoryOf(normalizedPath);
        return new SourceSubject(
            directoryPath,
            fact with
            {
                SourceFilePath = normalizedPath,
                FileNameWithoutExtension = GetFileNameWithoutExtension(normalizedPath),
                FolderSegments = GetFolderSegments(normalizedPath),
            });
    }

    private static string Combine(string scope, string path)
    {
        string normalizedScope = Normalize(scope);
        string normalizedPath = Normalize(path);
        return normalizedPath == "."
            ? normalizedScope
            : normalizedScope == "."
                ? normalizedPath
                : $"{normalizedScope}/{normalizedPath}";
    }

    private static ArchitectureApplicabilityRecord Evaluable(
        string controlIdentity,
        ArchitectureApplicabilityProvenance provenance) => new(
            controlIdentity,
            Family,
            ArchitectureApplicabilityRecordState.Evaluable,
            provenance);

    private static ArchitectureApplicabilityRecord Unassessable(
        string controlIdentity,
        string reasonCode,
        ArchitectureApplicabilityProvenance provenance) => new(
            controlIdentity,
            Family,
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason(reasonCode, provenance)],
            provenance);

    private static string DirectoryOf(string sourceFilePath)
    {
        int slash = sourceFilePath.LastIndexOf('/');
        return slash < 0 ? "." : sourceFilePath[..slash];
    }

    private static string GetFileNameWithoutExtension(string normalizedPath)
    {
        int slash = normalizedPath.LastIndexOf('/');
        string fileName = slash < 0 ? normalizedPath : normalizedPath[(slash + 1)..];
        int dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static string[] GetFolderSegments(string normalizedPath)
    {
        int slash = normalizedPath.LastIndexOf('/');
        return slash <= 0 ? [] : normalizedPath[..slash].Split('/');
    }

    private static string Normalize(string path)
    {
        string normalized = path.Replace('\\', '/').Trim().TrimEnd('/');
        return normalized.Length == 0 ? "." : normalized;
    }

    private static bool IsSameOrDescendant(string path, string prefix) =>
        string.Equals(path, prefix, StringComparison.Ordinal)
        || (prefix != "." && path.StartsWith(prefix + "/", StringComparison.Ordinal))
        || prefix == ".";

    private sealed record SourceSubject(string DirectoryPath, ArchitectureDeclaredTypeFact Fact);

    internal sealed record Result(
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ExpectedEntries,
        IReadOnlyList<ArchitectureApplicabilityRecord> Records);
}
