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
            .Select(c => new ArchitectureBaselineComparisonEntry(c.ContractGroup, c.ContractId!, c.SourceType, c.ForbiddenReference, reason))
            .ToList();

        return BuildFromEntries(entries);
    }

    public ArchitectureBaselineDocument BuildFromEntries(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
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

        var entriesByGroup = new Dictionary<string, Dictionary<string, List<ArchitectureIgnoredViolation>>>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            if (!entriesByGroup.TryGetValue(entry.ContractGroup, out var groupEntries))
            {
                groupEntries = new Dictionary<string, List<ArchitectureIgnoredViolation>>(StringComparer.Ordinal);
                entriesByGroup[entry.ContractGroup] = groupEntries;
            }

            if (!groupEntries.TryGetValue(entry.ContractId, out var violations))
            {
                violations = new List<ArchitectureIgnoredViolation>();
                groupEntries[entry.ContractId] = violations;
            }

            var ignoreEntry = new ArchitectureIgnoredViolation
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

        return baseline;
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
