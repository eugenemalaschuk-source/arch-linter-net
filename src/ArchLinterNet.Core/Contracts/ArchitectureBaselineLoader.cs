using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public static class ArchitectureBaselineLoader
{
    public static ArchitectureBaselineDocument LoadFromPath(string baselinePath)
    {
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException($"Baseline file not found: {baselinePath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = File.ReadAllText(baselinePath);
        ArchitectureBaselineDocument? document = deserializer.Deserialize<ArchitectureBaselineDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize baseline YAML.");
        }

        Validate(document);
        return document;
    }

    private static void Validate(ArchitectureBaselineDocument document)
    {
        if (document.Version != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported baseline version: {document.Version}. Only version 1 is supported.");
        }

        ValidateGroupEntries(document.Baseline.Strict, "strict");
        ValidateGroupEntries(document.Baseline.Audit, "audit");
        ValidateGroupEntries(document.Baseline.StrictLayers, "strict_layers");
        ValidateGroupEntries(document.Baseline.AuditLayers, "audit_layers");
        ValidateGroupEntries(document.Baseline.StrictAllowOnly, "strict_allow_only");
        ValidateGroupEntries(document.Baseline.AuditAllowOnly, "audit_allow_only");
        ValidateGroupEntries(document.Baseline.StrictCycles, "strict_cycles");
        ValidateGroupEntries(document.Baseline.AuditCycles, "audit_cycles");
        ValidateGroupEntries(document.Baseline.StrictAcyclicSiblings, "strict_acyclic_siblings");
        ValidateGroupEntries(document.Baseline.AuditAcyclicSiblings, "audit_acyclic_siblings");
        ValidateGroupEntries(document.Baseline.StrictMethodBody, "strict_method_body");
        ValidateGroupEntries(document.Baseline.AuditMethodBody, "audit_method_body");
        ValidateGroupEntries(document.Baseline.StrictIndependence, "strict_independence");
        ValidateGroupEntries(document.Baseline.AuditIndependence, "audit_independence");
        ValidateGroupEntries(document.Baseline.StrictProtected, "strict_protected");
        ValidateGroupEntries(document.Baseline.AuditProtected, "audit_protected");
        ValidateGroupEntries(document.Baseline.StrictExternal, "strict_external");
        ValidateGroupEntries(document.Baseline.AuditExternal, "audit_external");
        ValidateGroupEntries(document.Baseline.StrictCoverage, "strict_coverage");
        ValidateGroupEntries(document.Baseline.AuditCoverage, "audit_coverage");
    }

    private static void ValidateGroupEntries(List<ArchitectureBaselineContractEntry> entries, string groupName)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                throw new InvalidOperationException(
                    $"Baseline entry at index {i} in group '{groupName}' has an empty or missing 'id'. " +
                    "Each baseline entry must reference a contract by its 'id'.");
            }

            for (int j = 0; j < entry.IgnoredViolations.Count; j++)
            {
                var ignore = entry.IgnoredViolations[j];
                if (string.IsNullOrWhiteSpace(ignore.SourceType))
                {
                    throw new InvalidOperationException(
                        $"Baseline entry '{entry.Id}' in group '{groupName}' has an ignored_violations entry " +
                        $"at index {j} with an empty or missing 'source_type'.");
                }

                if (string.IsNullOrWhiteSpace(ignore.ForbiddenReference))
                {
                    throw new InvalidOperationException(
                        $"Baseline entry '{entry.Id}' in group '{groupName}' has an ignored_violations entry " +
                        $"at index {j} with an empty or missing 'forbidden_reference'.");
                }
            }
        }
    }
}
