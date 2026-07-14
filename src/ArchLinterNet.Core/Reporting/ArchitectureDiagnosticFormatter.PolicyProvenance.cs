using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static void ApplyPolicyLocationFields(
        ArchitectureDiagnostic diagnostic,
        Dictionary<string, object?> target)
    {
        if (diagnostic.PolicyLocation is not null)
        {
            target["policy_location"] = FormatPolicyLocationForJson(diagnostic.PolicyLocation);
        }

        if (diagnostic.RelatedPolicyLocations.Count > 0)
        {
            target["related_policy_locations"] = diagnostic.RelatedPolicyLocations
                .OrderBy(location => location.SourceOrdinal)
                .ThenBy(location => location.EncounterOrdinal)
                .Select(FormatPolicyLocationForJson)
                .ToArray();
        }
    }

    public static Dictionary<string, object?> FormatPolicyLocationForJson(
        ArchitecturePolicySourceLocation location)
    {
        var result = new Dictionary<string, object?>
        {
            ["root_path"] = location.RootPath,
            ["source_path"] = location.SourcePath,
            ["role"] = location.Role.ToString().ToLowerInvariant(),
            ["yaml_path"] = location.YamlPath,
            ["line"] = location.Line,
            ["column"] = location.Column,
            ["source_ordinal"] = location.SourceOrdinal,
            ["import_chain"] = location.Source.ImportChain.ToArray()
        };
        if (location.Source.DeclaringSourcePath is not null)
        {
            result["declaring_source_path"] = location.Source.DeclaringSourcePath;
        }

        if (location.Source.AuthoredImportPath is not null)
        {
            result["authored_import_path"] = location.Source.AuthoredImportPath;
        }

        if (location.ContractFamily is not null)
        {
            result["contract_family"] = location.ContractFamily;
        }

        if (location.ContractId is not null)
        {
            result["contract_id"] = location.ContractId;
        }

        return result;
    }

    private static string FormatPolicyLocationSuffix(ArchitectureDiagnostic diagnostic)
    {
        if (diagnostic.PolicyLocation is null)
        {
            return string.Empty;
        }

        ArchitecturePolicySourceLocation location = diagnostic.PolicyLocation;
        string root = string.Equals(location.SourcePath, location.RootPath, StringComparison.Ordinal)
            ? string.Empty
            : $"; root: {location.RootPath}";
        string related = diagnostic.RelatedPolicyLocations.Count == 0
            ? string.Empty
            : "; related: " + string.Join(", ", diagnostic.RelatedPolicyLocations
                .OrderBy(item => item.SourceOrdinal)
                .ThenBy(item => item.EncounterOrdinal)
                .Select(item => $"{item.SourcePath}:{item.YamlPath}"));
        return $" (policy: {location.SourcePath}:{location.YamlPath}{root}{related})";
    }
}
