using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

internal sealed class ArchitectureBaselineGenerator : IArchitectureBaselineGenerator
{
    public ArchitectureBaselineDocument Generate(
        ArchitectureContractDocument policyDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string reason = "generated baseline")
    {
        var entries = candidates
            .Where(c => c.ContractId != null)
            .Select(c => new ArchitectureBaselineComparisonEntry(
                c.ContractGroup, c.ContractId!, c.SourceType, c.ForbiddenReference, reason,
                c.Identity ?? BuildFallbackIdentity(c)))
            .ToList();

        return BuildFromEntries(entries, version: ArchitectureViolationIdentity.CurrentVersion);
    }

    public ArchitectureBaselineDocument BuildFromEntries(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries, int version = 2)
    {
        return version == 1 ? BuildLegacyDocument(entries) : BuildStructuredDocument(entries);
    }

    private static ArchitectureBaselineDocument BuildLegacyDocument(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
    {
        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups()
        };

        var ordered = entries
            .OrderBy(e => e.ContractGroup, StringComparer.Ordinal)
            .ThenBy(e => e.ContractId, StringComparer.Ordinal)
            .ThenBy(e => e.SourceType, StringComparer.Ordinal)
            .ThenBy(e => e.ForbiddenReference, StringComparer.Ordinal)
            .ToList();

        var entriesByGroup = new Dictionary<string, Dictionary<string, List<ArchitectureBaselineIgnoredViolation>>>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            var groupEntries = GetOrAddGroup(entriesByGroup, entry.ContractGroup);
            var violations = GetOrAddContract(groupEntries, entry.ContractId);

            var ignoreEntry = new ArchitectureBaselineIgnoredViolation
            {
                SourceType = entry.SourceType,
                ForbiddenReference = entry.ForbiddenReference,
                Reason = entry.Reason ?? "generated baseline"
            };

            string key = $"{entry.SourceType}|{entry.ForbiddenReference}";
            if (!violations.Any(v => $"{v.SourceType}|{v.ForbiddenReference}" == key))
            {
                violations.Add(ignoreEntry);
            }
        }

        PopulateGroups(baseline, entriesByGroup);
        return baseline;
    }

    private static ArchitectureBaselineDocument BuildStructuredDocument(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
    {
        var baseline = new ArchitectureBaselineDocument
        {
            Version = ArchitectureViolationIdentity.CurrentVersion,
            Baseline = new ArchitectureBaselineContractGroups()
        };

        var ordered = entries
            .Select(e => (Entry: e, Identity: e.Identity ?? BuildFallbackIdentity(e)))
            .OrderBy(x => x.Entry.ContractGroup, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.ContractId, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.SourceType, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.ForbiddenReference, StringComparer.Ordinal)
            .ThenBy(x => x.Identity.Occurrence)
            .ToList();

        var entriesByGroup = new Dictionary<string, Dictionary<string, List<ArchitectureBaselineIgnoredViolation>>>(StringComparer.Ordinal);
        var seenIdentities = new HashSet<ArchitectureViolationIdentity>();

        foreach (var (entry, identity) in ordered)
        {
            if (!seenIdentities.Add(identity))
            {
                continue;
            }

            var groupEntries = GetOrAddGroup(entriesByGroup, entry.ContractGroup);
            var violations = GetOrAddContract(groupEntries, entry.ContractId);
            violations.Add(ArchitectureBaselineIgnoredViolation.FromIdentity(
                identity, entry.SourceType, entry.ForbiddenReference, entry.Reason ?? "generated baseline"));
        }

        PopulateGroups(baseline, entriesByGroup);
        return baseline;
    }

    private static ArchitectureViolationIdentity BuildFallbackIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(entry.ContractGroup);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            contractFamily,
            ArchitectureViolationIdentity.ResolveKind(contractFamily),
            entry.ContractId,
            SourceAssembly: null,
            entry.SourceType,
            SourceMember: null,
            TargetAssembly: null,
            TargetType: null,
            entry.ForbiddenReference,
            Occurrence: 0);
    }

    private static ArchitectureViolationIdentity BuildFallbackIdentity(ArchitectureBaselineCandidate candidate)
    {
        string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(candidate.ContractGroup);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            contractFamily,
            ArchitectureViolationIdentity.ResolveKind(contractFamily),
            candidate.ContractId ?? string.Empty,
            SourceAssembly: null,
            candidate.SourceType,
            SourceMember: null,
            TargetAssembly: null,
            TargetType: null,
            candidate.ForbiddenReference,
            Occurrence: 0);
    }

    private static Dictionary<string, List<ArchitectureBaselineIgnoredViolation>> GetOrAddGroup(
        Dictionary<string, Dictionary<string, List<ArchitectureBaselineIgnoredViolation>>> entriesByGroup, string contractGroup)
    {
        if (!entriesByGroup.TryGetValue(contractGroup, out var groupEntries))
        {
            groupEntries = new Dictionary<string, List<ArchitectureBaselineIgnoredViolation>>(StringComparer.Ordinal);
            entriesByGroup[contractGroup] = groupEntries;
        }

        return groupEntries;
    }

    private static List<ArchitectureBaselineIgnoredViolation> GetOrAddContract(
        Dictionary<string, List<ArchitectureBaselineIgnoredViolation>> groupEntries, string contractId)
    {
        if (!groupEntries.TryGetValue(contractId, out var violations))
        {
            violations = new List<ArchitectureBaselineIgnoredViolation>();
            groupEntries[contractId] = violations;
        }

        return violations;
    }

    private static void PopulateGroups(
        ArchitectureBaselineDocument baseline,
        Dictionary<string, Dictionary<string, List<ArchitectureBaselineIgnoredViolation>>> entriesByGroup)
    {
        var groups = baseline.Baseline;
        foreach (var (groupName, groupEntries) in entriesByGroup)
        {
            var list = groupEntries
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => new ArchitectureBaselineContractEntry
                {
                    Id = e.Key,
                    IgnoredViolations = e.Value
                })
                .ToList();

            groups.SetGroup(groupName, list);
        }
    }

    public string Serialize(ArchitectureBaselineDocument document)
    {
        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
            .Build();

        return serializer.Serialize(document);
    }
}
