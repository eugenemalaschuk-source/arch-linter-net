using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public static class ArchitectureBaselineGenerator
{
    public static ArchitectureBaselineDocument Generate(
        ArchitectureContractDocument policyDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string reason = "generated baseline")
    {
        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups()
        };

        var ordered = candidates
            .OrderBy(c => c.ContractGroup, StringComparer.Ordinal)
            .ThenBy(c => c.ContractId, StringComparer.Ordinal)
            .ThenBy(c => c.SourceType, StringComparer.Ordinal)
            .ThenBy(c => c.ForbiddenReference, StringComparer.Ordinal)
            .ToList();

        var entriesByGroup = new Dictionary<string, Dictionary<string, List<ArchitectureIgnoredViolation>>>(StringComparer.Ordinal);

        foreach (var candidate in ordered)
        {
            if (candidate.ContractId == null)
            {
                continue;
            }

            if (!entriesByGroup.TryGetValue(candidate.ContractGroup, out var groupEntries))
            {
                groupEntries = new Dictionary<string, List<ArchitectureIgnoredViolation>>(StringComparer.Ordinal);
                entriesByGroup[candidate.ContractGroup] = groupEntries;
            }

            if (!groupEntries.TryGetValue(candidate.ContractId, out var violations))
            {
                violations = new List<ArchitectureIgnoredViolation>();
                groupEntries[candidate.ContractId] = violations;
            }

            var ignoreEntry = new ArchitectureIgnoredViolation
            {
                SourceType = candidate.SourceType,
                ForbiddenReference = candidate.ForbiddenReference,
                Reason = reason
            };

            string key = $"{candidate.SourceType}|{candidate.ForbiddenReference}";
            if (!violations.Any(v => $"{v.SourceType}|{v.ForbiddenReference}" == key))
            {
                violations.Add(ignoreEntry);
            }
        }

        var groups = baseline.Baseline;
        foreach (var (groupName, entries) in entriesByGroup)
        {
            var list = entries
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => new ArchitectureBaselineContractEntry
                {
                    Id = e.Key,
                    IgnoredViolations = e.Value
                })
                .ToList();

            SetGroupEntries(groups, groupName, list);
        }

        return baseline;
    }

    public static string Serialize(ArchitectureBaselineDocument document)
    {
        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(document);
    }

    private static void SetGroupEntries(ArchitectureBaselineContractGroups groups, string groupName, List<ArchitectureBaselineContractEntry> entries)
    {
        switch (groupName)
        {
            case "strict": groups.Strict = entries; break;
            case "audit": groups.Audit = entries; break;
            case "strict_layers": groups.StrictLayers = entries; break;
            case "audit_layers": groups.AuditLayers = entries; break;
            case "strict_allow_only": groups.StrictAllowOnly = entries; break;
            case "audit_allow_only": groups.AuditAllowOnly = entries; break;
            case "strict_cycles": groups.StrictCycles = entries; break;
            case "audit_cycles": groups.AuditCycles = entries; break;
            case "strict_acyclic_siblings": groups.StrictAcyclicSiblings = entries; break;
            case "audit_acyclic_siblings": groups.AuditAcyclicSiblings = entries; break;
            case "strict_method_body": groups.StrictMethodBody = entries; break;
            case "audit_method_body": groups.AuditMethodBody = entries; break;
            case "strict_independence": groups.StrictIndependence = entries; break;
            case "audit_independence": groups.AuditIndependence = entries; break;
            case "strict_protected": groups.StrictProtected = entries; break;
            case "audit_protected": groups.AuditProtected = entries; break;
            case "strict_external": groups.StrictExternal = entries; break;
            case "audit_external": groups.AuditExternal = entries; break;
        }
    }
}
