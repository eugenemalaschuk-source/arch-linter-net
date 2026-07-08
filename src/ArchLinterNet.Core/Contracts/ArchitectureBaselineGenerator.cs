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

            SetGroupEntries(groups, groupName, list);
        }

        return baseline;
    }

    public string Serialize(ArchitectureBaselineDocument document)
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
            case "strict_project_metadata": groups.StrictProjectMetadata = entries; break;
            case "audit_project_metadata": groups.AuditProjectMetadata = entries; break;
            case "strict_coverage": groups.StrictCoverage = entries; break;
            case "audit_coverage": groups.AuditCoverage = entries; break;
        }
    }
}
