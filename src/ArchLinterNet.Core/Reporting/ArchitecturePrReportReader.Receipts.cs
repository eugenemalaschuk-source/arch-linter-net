using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitecturePrReportDebtReceiptParser;
using static ArchLinterNet.Core.Reporting.ArchitecturePrReportReader;

namespace ArchLinterNet.Core.Reporting;

internal static class ArchitecturePrReportReceiptParser
{
    internal static ArchitecturePolicyInventory ReadPolicyInventory(JsonElement element)
    {
        string schema = RequiredString(element, "schema");
        if (!string.Equals(schema, ArchitecturePolicyInventory.CurrentSchemaId, StringComparison.Ordinal))
        {
            throw InvalidArtifact($"Unsupported policy inventory schema '{schema}'.");
        }

        JsonElement rules = Required(element, "rules", JsonValueKind.Object);
        JsonElement debt = Required(element, "ignore_debt", JsonValueKind.Object);
        JsonElement waivers = Required(element, "waivers", JsonValueKind.Array);
        return new ArchitecturePolicyInventory(
            schema,
            RequiredInt(element, "effective_rule_count"),
            new ArchitecturePolicyInventoryRules(
                RequiredInt(rules, "strict"),
                RequiredInt(rules, "audit"),
                RequiredInt(rules, "coverage")),
            new ArchitecturePolicyInventoryIgnoreDebt(
                RequiredInt(debt, "total"),
                RequiredInt(debt, "active"),
                RequiredInt(debt, "stale"),
                RequiredInt(debt, "expired"),
                RequiredInt(debt, "metadata_incomplete"),
                RequiredInt(debt, "invalid")),
            waivers.EnumerateArray().Select(ReadWaiver).ToArray());
    }

    private static ArchitectureWaiverLifecycleRecord ReadWaiver(JsonElement element)
    {
        RequireObject(element, "A waiver lifecycle record");
        return new ArchitectureWaiverLifecycleRecord(
            RequiredString(element, "id"),
            RequiredString(element, "state"),
            RequiredString(element, "contract"),
            OptionalString(element, "contract_id"),
            RequiredString(element, "contract_group"),
            RequiredString(element, "source_type"),
            RequiredString(element, "forbidden_reference"),
            OptionalString(element, "target_fingerprint"),
            RequiredString(element, "reason"),
            OptionalString(element, "owner"),
            OptionalString(element, "issue"),
            OptionalDate(element, "introduced"),
            OptionalDate(element, "expires"),
            RequiredDate(element, "evaluation_date"),
            RequiredBool(element, "matches_governed_finding"))
        {
            PolicyLocation = element.TryGetProperty("policy_location", out JsonElement location)
                && location.ValueKind != JsonValueKind.Null
                ? ReadPolicyLocation(location)
                : null,
        };
    }

    private static ArchitecturePolicySourceLocation ReadPolicyLocation(JsonElement element)
    {
        string role = RequiredString(element, "role");
        ArchitecturePolicyDocumentRole parsedRole = role switch
        {
            "root" => ArchitecturePolicyDocumentRole.Root,
            "fragment" => ArchitecturePolicyDocumentRole.Fragment,
            _ => throw InvalidArtifact($"Unsupported policy document role '{role}'."),
        };
        ArchitecturePolicySourceDescriptor source = new(
            RequiredString(element, "root_path"),
            RequiredString(element, "source_path"),
            parsedRole,
            RequiredInt(element, "source_ordinal"),
            OptionalString(element, "declaring_source_path"),
            OptionalString(element, "authored_import_path"),
            ReadStringArray(Required(element, "import_chain", JsonValueKind.Array)));
        return new ArchitecturePolicySourceLocation(
            source,
            RequiredString(element, "yaml_path"),
            RequiredInt(element, "line"),
            RequiredInt(element, "column"),
            OptionalString(element, "contract_family"),
            OptionalString(element, "contract_id"));
    }

    internal static ArchitectureWaiverLifecycleAssessment ReadWaiverLifecycle(JsonElement element)
    {
        JsonElement records = Required(element, "records", JsonValueKind.Array);
        return new ArchitectureWaiverLifecycleAssessment(
            RequiredString(element, "profile"),
            records.EnumerateArray().Select(ReadWaiver).ToArray(),
            ReadStringArray(Required(element, "blocking_states", JsonValueKind.Array)));
    }

    internal static ArchitecturePrReportApplicability ReadApplicability(JsonElement element)
    {
        JsonElement summary = Required(element, "summary", JsonValueKind.Object);
        JsonElement reasons = Required(element, "reasons", JsonValueKind.Array);
        JsonElement controls = Required(element, "controls", JsonValueKind.Array);
        return new ArchitecturePrReportApplicability(
            RequiredString(element, "state"),
            new ArchitecturePrReportApplicabilitySummary(
                RequiredInt(summary, "required"),
                RequiredInt(summary, "required_evaluable"),
                RequiredInt(summary, "required_unassessable")),
            reasons.EnumerateArray().Select(ReadApplicabilityReason).ToArray(),
            controls.EnumerateArray().Select(ReadApplicabilityControl).ToArray());
    }

    private static ArchitecturePrReportApplicabilityReason ReadApplicabilityReason(JsonElement element) =>
        new(RequiredString(element, "code"), ReadProvenanceReference(
            Required(element, "provenance", JsonValueKind.Object)));

    private static ArchitecturePrReportApplicabilityControl ReadApplicabilityControl(JsonElement element)
    {
        RequireObject(element, "An applicability control");
        ArchitecturePrReportApplicabilityExpected? expected = element.TryGetProperty("expected", out JsonElement expectedElement)
            ? ReadApplicabilityExpected(expectedElement)
            : null;
        ArchitecturePrReportApplicabilityRecord? record = element.TryGetProperty("record", out JsonElement recordElement)
            ? ReadApplicabilityRecord(recordElement)
            : null;
        JsonElement reasons = Required(element, "integrity_reasons", JsonValueKind.Array);
        return new ArchitecturePrReportApplicabilityControl(
            RequiredString(element, "control_identity"),
            OptionalString(element, "membership"),
            RequiredString(element, "state"),
            RequiredBool(element, "integrity_valid"),
            reasons.EnumerateArray().Select(ReadApplicabilityReason).ToArray(),
            expected,
            record);
    }

    private static ArchitecturePrReportApplicabilityExpected ReadApplicabilityExpected(JsonElement element) =>
        new(
            RequiredString(element, "control_identity"),
            RequiredString(element, "family"),
            RequiredString(element, "membership"),
            ReadProvenanceReference(Required(element, "provenance", JsonValueKind.Object)));

    private static ArchitecturePrReportApplicabilityRecord ReadApplicabilityRecord(JsonElement element)
    {
        return new ArchitecturePrReportApplicabilityRecord(
            RequiredString(element, "control_identity"),
            RequiredString(element, "family"),
            RequiredString(element, "state"),
            Required(element, "reasons", JsonValueKind.Array).EnumerateArray()
                .Select(ReadApplicabilityReason).ToArray(),
            ReadProvenanceReference(Required(element, "provenance", JsonValueKind.Object)),
            element.TryGetProperty("topology_evidence", out JsonElement topology)
                ? ReadTopology(topology)
                : null,
            element.TryGetProperty("metric_evidence", out JsonElement metric)
                ? ReadMetric(metric)
                : null);
    }

    private static ArchitecturePrReportTopology ReadTopology(JsonElement element)
    {
        JsonElement counts = Required(element, "counts", JsonValueKind.Object);
        return new ArchitecturePrReportTopology(
            RequiredString(element, "mode"),
            RequiredString(element, "subject_kind"),
            RequiredInt(element, "declared_component_count"),
            new ArchitecturePrReportTopologyCounts(
                RequiredInt(counts, "observed"),
                RequiredInt(counts, "mapped"),
                RequiredInt(counts, "reviewed_out_of_scope"),
                RequiredInt(counts, "unmapped"),
                RequiredInt(counts, "ambiguous")),
            Required(element, "subjects", JsonValueKind.Array).EnumerateArray()
                .Select(item => new ArchitecturePrReportTopologySubject(
                    RequiredString(item, "identity"), RequiredString(item, "project"),
                    RequiredString(item, "assembly"), RequiredString(item, "subject"),
                    RequiredString(item, "disposition"),
                    ReadStringArray(Required(item, "node_ids", JsonValueKind.Array)),
                    OptionalString(item, "reviewed_out_of_scope_id"))).ToArray(),
            Required(element, "relationships", JsonValueKind.Array).EnumerateArray()
                .Select(item => new ArchitecturePrReportTopologyRelation(
                    RequiredString(item, "source_node"), RequiredString(item, "target_node"),
                    RequiredString(item, "witness"), RequiredBool(item, "is_allowed"))).ToArray(),
            ReadStringArray(Required(element, "stale_nodes", JsonValueKind.Array)),
            Required(element, "stale_edges", JsonValueKind.Array).EnumerateArray()
                .Select(item => new ArchitecturePrReportTopologyEdge(
                    RequiredString(item, "source_node"), RequiredString(item, "target_node"))).ToArray());
    }

    private static ArchitecturePrReportMetric ReadMetric(JsonElement element) =>
        new(
            RequiredString(element, "metric_id"), RequiredString(element, "kind"),
            OptionalString(element, "native_subject"), OptionalString(element, "unit"),
            RequiredString(element, "effective_scope"), OptionalInt(element, "value"),
            element.TryGetProperty("contributors", out JsonElement contributors)
                && contributors.ValueKind != JsonValueKind.Null
                ? ReadStringArray(contributors)
                : null);

    internal static ArchitecturePrReportExternalEvidence ReadExternalEvidence(JsonElement element) =>
        new(
            RequiredString(element, "mode"),
            Required(element, "requirements", JsonValueKind.Array).EnumerateArray()
                .Select(ReadExternalRequirement).ToArray(),
            Required(element, "findings", JsonValueKind.Array).EnumerateArray()
                .Select(ReadFinding).ToArray());

    private static ArchitecturePrReportExternalRequirement ReadExternalRequirement(JsonElement element)
    {
        RequireObject(element, "An external-evidence requirement");
        ArchitecturePrReportDiagnosticFilter? filter = element.TryGetProperty("diagnostic_filter", out JsonElement filterElement)
            ? new ArchitecturePrReportDiagnosticFilter(
                ReadStringArray(Required(filterElement, "rule_ids", JsonValueKind.Array)),
                ReadStringArray(Required(filterElement, "rule_tags", JsonValueKind.Array)),
                ReadStringArray(Required(filterElement, "projects", JsonValueKind.Array)),
                ReadStringArray(Required(filterElement, "path_prefixes", JsonValueKind.Array)),
                ReadStringMap(Required(filterElement, "severity", JsonValueKind.Object)),
                RequiredBool(filterElement, "require_matches"))
            : null;
        return new ArchitecturePrReportExternalRequirement(
            RequiredString(element, "id"), RequiredString(element, "format"),
            RequiredBool(element, "required"), RequiredString(element, "tool"),
            OptionalString(element, "tool_version"), RequiredString(element, "run"),
            RequiredBool(element, "require_repository"), RequiredBool(element, "require_revision"),
            RequiredBool(element, "require_scope"), filter);
    }

    private static ArchitecturePrReportProvenanceReference ReadProvenanceReference(JsonElement element)
    {
        RequireObject(element, "A provenance reference");
        return new(OptionalString(element, "family"), OptionalString(element, "control_identity"),
            OptionalString(element, "policy_identity"), OptionalString(element, "evidence_identity"));
    }

    internal static ArchitecturePrReportProvenance ReadProvenance(JsonElement element) =>
        new(RequiredString(element, "repository_root"),
            ReadStringArray(Required(element, "policy_import_paths", JsonValueKind.Array)),
            ReadStringArray(Required(element, "resolved_assembly_paths", JsonValueKind.Array)),
            ReadStringArray(Required(element, "discovered_project_paths", JsonValueKind.Array)));
}
