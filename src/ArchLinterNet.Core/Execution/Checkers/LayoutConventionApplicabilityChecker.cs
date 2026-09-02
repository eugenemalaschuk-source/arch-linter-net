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

        // Built once and shared by both the source-declaration inventory and every convention
        // projection below. A partial type can have one reflected fact but several real source
        // declarations, so the source declaration, rather than the primary fact path, is the
        // inventory's unit of observation.
        Dictionary<(string AssemblyName, string FullTypeName), Type> typesByIdentity =
            LayoutConventionChecker.BuildTypeIdentityLookup(context);
        (SourceSubject[] subjects,
            Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> candidatesByFile) =
            BuildSubjects(context, inventory.Scope, typesByIdentity);

        HashSet<string>[] effectiveSubjectIdentities = linkedConventions
            .Select(convention => BuildEffectiveSubjectIdentities(convention, context, candidatesByFile))
            .ToArray();
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

            records.Add(folderSubjects.Any(subject => effectiveSubjectIdentities[index].Contains(subject.Identity))
                ? Evaluable(controlIdentity, provenance)
                : Unassessable(
                    controlIdentity,
                    ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput,
                    provenance));
        }

        SubjectMapping[] subjectMappings = subjects
            .SelectMany(subject => Enumerable.Range(0, inventory.ExpectedFolders.Count)
                .Where(index => IsSameOrDescendant(
                    subject.DirectoryPath,
                    Combine(inventory.Scope, inventory.ExpectedFolders[index].Path))
                    && effectiveSubjectIdentities[index].Contains(subject.Identity))
                .Select(index => new SubjectMapping(subject.Identity, linkedConventions[index].Id!)))
            .OrderBy(mapping => mapping.SubjectIdentity, StringComparer.Ordinal)
            .ThenBy(mapping => mapping.ConventionId, StringComparer.Ordinal)
            .ToArray();
        var subjectIssues = new List<SubjectIssue>();

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
                        && effectiveSubjectIdentities[index].Contains(subject.Identity))
                    .Select(index => linkedConventions[index].Id)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (distinctConventionMappings == 0)
                {
                    reasons.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
                    subjectIssues.Add(new SubjectIssue(
                        subject.Identity,
                        subject.Fact.SourceFilePath!,
                        ArchitectureApplicabilityReasonCodes.UnmappedSubject));
                }
                else if (distinctConventionMappings > 1)
                {
                    reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
                    subjectIssues.Add(new SubjectIssue(
                        subject.Identity,
                        subject.Fact.SourceFilePath!,
                        ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
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

        return new Result(
            expectedEntries,
            records,
            subjectMappings,
            subjectIssues
                .OrderBy(issue => issue.SubjectIdentity, StringComparer.Ordinal)
                .ThenBy(issue => issue.ReasonCode, StringComparer.Ordinal)
                .ToArray());
    }

    private static HashSet<string> BuildEffectiveSubjectIdentities(
        ArchitectureLayoutConventionContract convention,
        ArchitectureCheckerContext context,
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> candidatesByFile)
    {
        // ProjectFiledCandidateGroups is also consumed by LayoutConventionChecker. Keeping this
        // projection shared prevents the inventory from proving applicability with selector
        // semantics that the convention checker would never actually use.
        List<LayoutConventionChecker.LayoutFileGroup> groups =
            LayoutConventionChecker.ProjectFiledCandidateGroups(
                convention,
                context,
                candidatesByFile,
                new bool[convention.ExcludeFilesMatching.Count],
                out _);
        return groups
            .SelectMany(group => group.Facts.Select(fact => BuildSubjectIdentity(group.SourceFilePath!, fact)))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static (
        SourceSubject[] Subjects,
        Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>> CandidatesByFile)
        BuildSubjects(
            ArchitectureCheckerContext context,
            string scope,
            IReadOnlyDictionary<(string AssemblyName, string FullTypeName), Type> typesByIdentity)
    {
        string normalizedScope = Normalize(scope);
        var candidatesByFile = new Dictionary<string, List<(Type Type, ArchitectureDeclaredTypeFact Fact)>>(StringComparer.Ordinal);
        var subjects = new List<SourceSubject>();

        foreach (ArchitectureTypeSourceDeclaration declaration in context.SourceFileFactIndex.SourceDeclarations
                     .GroupBy(declaration => (
                         declaration.AssemblyName,
                         declaration.FullTypeName,
                         declaration.SourceFilePath))
                     .Select(group => group.First())
                     .OrderBy(declaration => declaration.SourceFilePath, StringComparer.Ordinal)
                     .ThenBy(declaration => declaration.FullTypeName, StringComparer.Ordinal)
                     .ThenBy(declaration => declaration.AssemblyName, StringComparer.Ordinal))
        {
            if (!context.SourceFileFactIndex.TryGetFact(
                    declaration.AssemblyName,
                    declaration.FullTypeName,
                    out ArchitectureDeclaredTypeFact fact)
                || !typesByIdentity.TryGetValue(
                    (declaration.AssemblyName, declaration.FullTypeName),
                    out Type? type))
            {
                continue;
            }

            SourceSubject subject = CreateSourceSubject(fact, declaration.SourceFilePath);
            if (!IsSameOrDescendant(subject.DirectoryPath, normalizedScope))
            {
                continue;
            }

            subjects.Add(subject);
            if (!candidatesByFile.TryGetValue(
                    subject.Fact.SourceFilePath!,
                    out List<(Type Type, ArchitectureDeclaredTypeFact Fact)>? entries))
            {
                entries = new List<(Type, ArchitectureDeclaredTypeFact)>();
                candidatesByFile[subject.Fact.SourceFilePath!] = entries;
            }

            entries.Add((type, subject.Fact));
        }

        return (
            subjects
                .OrderBy(subject => subject.DirectoryPath, StringComparer.Ordinal)
                .ThenBy(subject => subject.Fact.FullTypeName, StringComparer.Ordinal)
                .ThenBy(subject => subject.Fact.AssemblyName, StringComparer.Ordinal)
                .ThenBy(subject => subject.Fact.SourceFilePath, StringComparer.Ordinal)
                .ToArray(),
            candidatesByFile);
    }

    private static SourceSubject CreateSourceSubject(
        ArchitectureDeclaredTypeFact fact,
        string sourceFilePath)
    {
        string normalizedPath = Normalize(sourceFilePath);
        string directoryPath = DirectoryOf(normalizedPath);
        ArchitectureDeclaredTypeFact sourceFact = fact with
        {
            SourceFilePath = normalizedPath,
            FileNameWithoutExtension = GetFileNameWithoutExtension(normalizedPath),
            FolderSegments = GetFolderSegments(normalizedPath),
        };
        return new SourceSubject(
            directoryPath,
            sourceFact,
            BuildSubjectIdentity(normalizedPath, sourceFact));
    }

    private static string BuildSubjectIdentity(string sourceFilePath, ArchitectureDeclaredTypeFact fact) =>
        $"{sourceFilePath}|{fact.AssemblyName}|{fact.FullTypeName}";

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

    private sealed record SourceSubject(
        string DirectoryPath,
        ArchitectureDeclaredTypeFact Fact,
        string Identity);

    internal sealed record SubjectMapping(string SubjectIdentity, string ConventionId);

    internal sealed record SubjectIssue(
        string SubjectIdentity,
        string SourceFilePath,
        string ReasonCode);

    internal sealed record Result(
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ExpectedEntries,
        IReadOnlyList<ArchitectureApplicabilityRecord> Records,
        IReadOnlyList<SubjectMapping> SubjectMappings,
        IReadOnlyList<SubjectIssue> SubjectIssues);
}
