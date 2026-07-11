using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures)
    {
        if (conflicts.Count == 0 && metadataFailures.Count == 0)
        {
            return string.Empty;
        }

        var conflictLines = conflicts
            .OrderBy(c => c.Subject, StringComparer.Ordinal)
            .ThenBy(c => c.Source)
            .ThenBy(c => c.MetadataDetail, StringComparer.Ordinal)
            .Select(c => $"  conflict: [{c.Source}] {c.Subject}: kept '{c.WinningRole}', discarded '{c.DiscardedRole}'"
                + (c.MetadataDetail != null ? $" ({c.MetadataDetail})" : string.Empty));

        var failureLines = metadataFailures
            .OrderBy(f => f.Subject, StringComparer.Ordinal)
            .ThenBy(f => f.MetadataKey, StringComparer.Ordinal)
            .Select(f => $"  metadata_failure: [{f.Source}] {f.Subject}.{f.MetadataKey}: {f.Reason}");

        return "Classification findings:" + Environment.NewLine
            + string.Join(Environment.NewLine, conflictLines.Concat(failureLines));
    }

    private static object[] BuildClassificationConflictsJson(
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts)
    {
        return (classificationConflicts ?? Array.Empty<ArchitectureClassificationConflict>())
            .OrderBy(c => c.Subject, StringComparer.Ordinal)
            .ThenBy(c => c.MetadataDetail, StringComparer.Ordinal)
            .Select(c => (object)new
            {
                subject = c.Subject,
                source = c.Source.ToString(),
                winning_role = c.WinningRole,
                discarded_role = c.DiscardedRole,
                metadata_detail = c.MetadataDetail
            })
            .ToArray();
    }

    private static object[] BuildClassificationMetadataFailuresJson(
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures)
    {
        return (classificationMetadataFailures ?? Array.Empty<ArchitectureClassificationMetadataFailure>())
            .OrderBy(f => f.Subject, StringComparer.Ordinal)
            .ThenBy(f => f.MetadataKey, StringComparer.Ordinal)
            .Select(f => (object)new
            {
                subject = f.Subject,
                source = f.Source.ToString(),
                metadata_key = f.MetadataKey,
                reason = f.Reason
            })
            .ToArray();
    }

    private static object[] BuildClassificationRolesJson(
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? classificationRoles)
    {
        return (classificationRoles ?? Array.Empty<ArchitectureClassificationRoleFact>())
            .OrderBy(r => r.Subject, StringComparer.Ordinal)
            .Select(r => (object)new
            {
                subject = r.Subject,
                role = r.Role,
                source = r.Source.ToString(),
                metadata = r.Metadata
            })
            .ToArray();
    }
}
