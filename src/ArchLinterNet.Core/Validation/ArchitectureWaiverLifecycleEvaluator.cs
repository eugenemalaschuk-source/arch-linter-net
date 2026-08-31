using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureWaiverLifecycleEvaluator
{
    private const string Active = "active";
    private const string Expired = "expired";
    private const string Invalid = "invalid";
    private const string MetadataIncomplete = "metadata_incomplete";
    private const string Stale = "stale";

    internal static IReadOnlyList<ArchitectureWaiverLifecycleRecord> Evaluate(
        ArchitectureContractDocument document,
        string mode,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched,
        DateOnly evaluationDate,
        IReadOnlyCollection<string>? selectedContractIds = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(unmatched);

        ArchitectureContractDescriptor[] descriptors = ArchitectureContractCatalog.Build(document).Descriptors
            .Where(item => item.Mode == mode && IsSelected(item, selectedContractIds))
            .ToArray();
        var declarations = new Dictionary<ArchitectureIgnoredViolation, List<WaiverOccurrence>>(
            ReferenceEqualityComparer.Instance);

        foreach (ArchitectureContractDescriptor descriptor in descriptors)
        {
            int index = 0;
            foreach (ArchitectureIgnoredViolation ignore in GetIgnoredViolations(descriptor.Contract))
            {
                if (!ignore.IsBaselineImported)
                {
                    if (!declarations.TryGetValue(ignore, out List<WaiverOccurrence>? occurrences))
                    {
                        occurrences = [];
                        declarations.Add(ignore, occurrences);
                    }

                    occurrences.Add(new WaiverOccurrence(descriptor, index));
                }

                index++;
            }
        }

        var records = new List<ArchitectureWaiverLifecycleRecord>(declarations.Count);
        foreach ((ArchitectureIgnoredViolation ignore, List<WaiverOccurrence> occurrences) in declarations)
        {
            // A source-set alias is one execution instance of the authored declaration. Its
            // lifecycle state is stale only when every selected alias is unmatched; any matching
            // alias keeps the one canonical declaration active (or expired/invalid by precedence).
            bool isUnmatched = occurrences.All(occurrence => IsUnmatched(occurrence, unmatched));
            records.Add(CreateRecord(document, occurrences[0].Descriptor, ignore, isUnmatched, evaluationDate));
        }

        return records
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ThenBy(record => record.ContractGroup, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool HasManualWaivers(ArchitectureContractDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Contracts.AllStrict.Concat(document.Contracts.AllAudit)
            .SelectMany(GetIgnoredViolations)
            .Any(ignore => !ignore.IsBaselineImported);
    }

    private static bool IsSelected(
        ArchitectureContractDescriptor descriptor,
        IReadOnlyCollection<string>? selectedContractIds)
    {
        if (selectedContractIds is not { Count: > 0 })
        {
            return true;
        }

        return (descriptor.Id is not null
                && selectedContractIds.Contains(descriptor.Id, StringComparer.OrdinalIgnoreCase))
            || (descriptor.AuthoredId is not null
                && selectedContractIds.Contains(descriptor.AuthoredId, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsUnmatched(
        WaiverOccurrence occurrence,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched)
    {
        ArchitectureContractDescriptor descriptor = occurrence.Descriptor;
        return unmatched.Any(candidate => candidate.ContractName == descriptor.Name
            && candidate.ContractId == descriptor.Id
            && candidate.ContractGroup == descriptor.Group
            && candidate.IgnoreIndex == occurrence.IgnoreIndex);
    }

    private static ArchitectureWaiverLifecycleRecord CreateRecord(
        ArchitectureContractDocument document,
        ArchitectureContractDescriptor descriptor,
        ArchitectureIgnoredViolation ignore,
        bool isUnmatched,
        DateOnly evaluationDate)
    {
        DateOnly? introduced = TryParseDate(ignore.Introduced);
        DateOnly? expires = TryParseDate(ignore.Expires);
        string state = ignore.WaiverValidationError is not null
            ? Invalid
            : expires is { } expiry && expiry < evaluationDate
                ? Expired
                : isUnmatched
                    ? Stale
                    : ignore.HasStructuredWaiverFields
                        ? Active
                        : MetadataIncomplete;

        return new ArchitectureWaiverLifecycleRecord(
            ignore.WaiverId ?? CreateLegacyId(descriptor, ignore),
            state,
            descriptor.Name,
            descriptor.Id,
            descriptor.Group,
            ignore.SourceType,
            ignore.ForbiddenReference,
            ignore.Target?.Fingerprint,
            ignore.Reason,
            ignore.Owner,
            ignore.Issue,
            introduced,
            expires,
            evaluationDate,
            !isUnmatched)
        {
            PolicyLocation = document.Provenance.LocationFor(ignore),
        };
    }

    private static IEnumerable<ArchitectureIgnoredViolation> GetIgnoredViolations(IArchitectureContract contract)
    {
        PropertyInfo? property = contract.GetType().GetProperty("IgnoredViolations");
        return property?.GetValue(contract) as IEnumerable<ArchitectureIgnoredViolation>
            ?? Array.Empty<ArchitectureIgnoredViolation>();
    }

    private static DateOnly? TryParseDate(string? value) => DateOnly.TryParseExact(
        value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
        ? date
        : null;

    private static string CreateLegacyId(ArchitectureContractDescriptor descriptor, ArchitectureIgnoredViolation ignore)
    {
        string source = string.Join("\n", descriptor.Group, descriptor.Id ?? descriptor.Name, ignore.SourceType, ignore.ForbiddenReference);
        return "legacy-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..16];
    }

    private sealed record WaiverOccurrence(ArchitectureContractDescriptor Descriptor, int IgnoreIndex);
}
