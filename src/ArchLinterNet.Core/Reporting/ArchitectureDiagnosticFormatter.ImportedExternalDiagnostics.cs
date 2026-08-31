using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static string FormatImportedExternalDiagnosticForHumans(ImportedExternalDiagnostic diagnostic)
    {
        SarifEvidenceSourceDiagnostic source = diagnostic.SourceDiagnostic;
        SarifEvidenceSourceLocation? location = source.PrimaryLocation;
        string tool = diagnostic.EvidenceProvenances[0].ToolName ?? "<unknown-tool>";
        string rule = source.RuleId ?? "<unknown-rule>";
        string message = source.Message ?? "<no source message>";
        string path = location?.Path ?? "<no source location>";
        string region = FormatRegion(location?.Region);
        string fingerprint = diagnostic.Fingerprint.Origin == SarifExternalDiagnosticFingerprintOrigin.Source
            ? $"source:{diagnostic.Fingerprint.SourceName}:{diagnostic.Fingerprint.Value}"
            : $"deterministic:{diagnostic.Fingerprint.Value}";
        string evidence = string.Join(
            "; ",
            diagnostic.EvidenceProvenances.Select(FormatEvidenceProvenanceForHumans));

        return $"- [{diagnostic.LogicalEvidenceId}] [imported_external_diagnostic] {tool}/{rule} at {path}{region}: "
            + $"{message} (source_severity={SourceSeverityToken(source.SourceSeverity)}, "
            + $"governance_mode={GovernanceModeToken(diagnostic.GovernanceMode)}, fingerprint={fingerprint}; "
            + $"evidence=[{evidence}])";
    }

    private static string FormatEvidenceProvenanceForHumans(SarifEvidenceProvenance provenance)
    {
        SarifEvidenceResolvedContext? context = provenance.Context;
        return $"logical={provenance.LogicalId}, tool={provenance.ToolName ?? "<unknown>"}, "
            + $"version={provenance.ToolVersion ?? "<unknown>"}, run={provenance.RunId ?? "<unknown>"}, "
            + $"repository={context?.Repository ?? "<unknown>"}, revision={context?.Revision ?? "<unknown>"}, "
            + $"scope={context?.Scope ?? "<unknown>"}, artifact={provenance.ArtifactPath ?? "<unknown>"}, "
            + $"sha256={provenance.ArtifactSha256 ?? "<unknown>"}";
    }

    private static string FormatRegion(SarifEvidenceSourceRegion? region)
    {
        if (region?.StartLine is not { } line)
        {
            return string.Empty;
        }

        return region.StartColumn is { } column ? $":{line}:{column}" : $":{line}";
    }

    private static void ApplyImportedExternalDiagnosticCiFields(
        ImportedExternalDiagnostic diagnostic,
        Dictionary<string, object?> obj)
    {
        SarifEvidenceSourceDiagnostic source = diagnostic.SourceDiagnostic;
        SarifEvidenceSourceLocation? location = source.PrimaryLocation;
        obj["logical_evidence_id"] = diagnostic.LogicalEvidenceId;
        obj["selected_diagnostic_identity"] = diagnostic.SelectedCanonicalIdentity;
        obj["governance_mode"] = GovernanceModeToken(diagnostic.GovernanceMode);
        obj["source_diagnostic"] = new Dictionary<string, object?>
        {
            ["tool"] = diagnostic.EvidenceProvenances[0].ToolName,
            ["rule_id"] = source.RuleId,
            ["message"] = source.Message,
            ["severity"] = SourceSeverityToken(source.SourceSeverity),
            ["project"] = source.Project,
            ["driver_rule_tags"] = source.DriverRuleTags.ToArray(),
            ["location"] = FormatSourceLocation(location),
            ["fingerprint"] = new Dictionary<string, object?>
            {
                ["origin"] = diagnostic.Fingerprint.Origin == SarifExternalDiagnosticFingerprintOrigin.Source
                    ? "source"
                    : "deterministic",
                ["name"] = diagnostic.Fingerprint.SourceName,
                ["value"] = diagnostic.Fingerprint.Value,
            },
        };
        obj["evidence_provenance"] = diagnostic.EvidenceProvenances
            .Select(provenance => (object)FormatEvidenceProvenanceForJson(provenance))
            .ToArray();
    }

    private static Dictionary<string, object?>? FormatSourceLocation(SarifEvidenceSourceLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        SarifEvidenceSourceRegion? region = location.Region;
        return new Dictionary<string, object?>
        {
            ["path"] = location.Path,
            ["start_line"] = region?.StartLine,
            ["start_column"] = region?.StartColumn,
            ["end_line"] = region?.EndLine,
            ["end_column"] = region?.EndColumn,
            ["char_offset"] = region?.CharOffset,
            ["char_length"] = region?.CharLength,
        };
    }

    private static Dictionary<string, object?> FormatEvidenceProvenanceForJson(SarifEvidenceProvenance provenance) => new()
    {
        ["logical_evidence_id"] = provenance.LogicalId,
        ["tool"] = provenance.ToolName,
        ["tool_version"] = provenance.ToolVersion,
        ["run_id"] = provenance.RunId,
        ["artifact_path"] = provenance.ArtifactPath,
        ["artifact_sha256"] = provenance.ArtifactSha256,
        ["result_count"] = provenance.ResultCount,
        ["repository"] = provenance.Context?.Repository,
        ["revision"] = provenance.Context?.Revision,
        ["scope"] = provenance.Context?.Scope,
    };

    private static string SourceSeverityToken(SarifEvidenceSourceSeverity severity) => severity switch
    {
        SarifEvidenceSourceSeverity.Error => "error",
        SarifEvidenceSourceSeverity.Warning => "warning",
        SarifEvidenceSourceSeverity.Note => "note",
        SarifEvidenceSourceSeverity.None => "none",
        SarifEvidenceSourceSeverity.Unspecified => "unspecified",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown source severity."),
    };

    private static string GovernanceModeToken(SarifExternalDiagnosticGovernanceMode mode) => mode switch
    {
        SarifExternalDiagnosticGovernanceMode.Strict => "strict",
        SarifExternalDiagnosticGovernanceMode.Audit => "audit",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown imported diagnostic governance mode."),
    };
}
