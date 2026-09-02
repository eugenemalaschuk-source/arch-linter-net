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
        SourceSubject[] subjects = BuildSubjects(context.SourceFileFactIndex.AllFacts, inventory.Scope);
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
                    linkedConventions[index], subject.Fact, context))
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
                        && MatchesConvention(linkedConventions[index], subject.Fact, context))
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
        IReadOnlyList<ArchitectureDeclaredTypeFact> facts,
        string scope)
    {
        string normalizedScope = Normalize(scope);
        return facts
            .Where(fact => fact.SourceFilePath is not null)
            .Select(fact => new SourceSubject(DirectoryOf(fact.SourceFilePath!), fact))
            .Where(subject => IsSameOrDescendant(subject.DirectoryPath, normalizedScope))
            .OrderBy(subject => subject.DirectoryPath, StringComparer.Ordinal)
            .ThenBy(subject => subject.Fact.FullTypeName, StringComparer.Ordinal)
            .ThenBy(subject => subject.Fact.AssemblyName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool MatchesConvention(
        ArchitectureLayoutConventionContract convention,
        ArchitectureDeclaredTypeFact fact,
        ArchitectureCheckerContext context)
    {
        if (!MatchesMatcher(convention.FilesMatching, fact, context))
        {
            return false;
        }

        return !convention.ExcludeFilesMatching.Any(matcher => MatchesMatcher(matcher, fact, context));
    }

    private static bool MatchesMatcher(
        ArchitectureLayoutFileMatcher matcher,
        ArchitectureDeclaredTypeFact fact,
        ArchitectureCheckerContext context)
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

        if (matcher.CompiledWhen is null)
        {
            return true;
        }

        Type? type = context.TypeIndex.AllTypes().FirstOrDefault(candidate =>
            string.Equals(candidate.Assembly.GetName().Name, fact.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.FullName, fact.FullTypeName, StringComparison.Ordinal));
        return type is not null && LayoutConventionChecker.MatchesWhenForSourceType(
            matcher,
            context,
            fact.AssemblyName,
            fact.FullTypeName,
            new Dictionary<(string AssemblyName, string FullTypeName), Type>
            {
                [(fact.AssemblyName, fact.FullTypeName)] = type,
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
