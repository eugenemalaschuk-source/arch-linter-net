using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitecturePrReportReader;

namespace ArchLinterNet.Core.Reporting;

internal static class ArchitecturePrReportDebtReceiptParser
{
    internal static ArchitecturePrReportFinding ReadFinding(JsonElement element)
    {
        RequireObject(element, "A report finding");
        JsonElement? sourceLocation = element.TryGetProperty("source_location", out JsonElement source)
            && source.ValueKind != JsonValueKind.Null
            ? source
            : null;
        JsonElement? remediation = element.TryGetProperty("remediation_guidance", out JsonElement guidance)
            && guidance.ValueKind != JsonValueKind.Null
            ? guidance
            : null;
        JsonElement? policyOrigin = element.TryGetProperty("policy_origin", out JsonElement origin)
            && origin.ValueKind != JsonValueKind.Null
            ? origin
            : null;
        return new ArchitecturePrReportFinding(
            RequiredInt(element, "schema_version"),
            RequiredString(element, "kind"),
            RequiredString(element, "canonical_identity"),
            OptionalString(element, "mode"),
            OptionalString(element, "severity"),
            RequiredString(element, "message_code"),
            RequiredString(element, "contract"),
            OptionalString(element, "contract_id"),
            PolicyIdentity(policyOrigin),
            sourceLocation is null ? null : ReadSourceLocation(sourceLocation.Value),
            remediation is null ? null : ReadRemediation(remediation.Value),
            element.TryGetProperty("details", out JsonElement details) ? details.Clone() : default);
    }

    private static ArchitecturePrReportSourceLocation ReadSourceLocation(JsonElement element) =>
        new(RequiredString(element, "path"), OptionalInt(element, "line"), OptionalInt(element, "column"));

    private static ArchitecturePrReportRemediation ReadRemediation(JsonElement element)
    {
        JsonElement findingIdentity = Required(element, "finding_identity", JsonValueKind.Object);
        return new ArchitecturePrReportRemediation(
            RequiredString(element, "category"),
            RequiredString(element, "summary"),
            RequiredString(element, "contract_identity"),
            findingIdentity.GetRawText(),
            Required(element, "evidence", JsonValueKind.Array).EnumerateArray()
                .Select(item => new ArchitecturePrReportEvidenceFact(
                    RequiredString(item, "kind"), RequiredString(item, "value"))).ToArray(),
            OptionalString(element, "expected_seam_or_direction"),
            OptionalString(element, "caveat"),
            RequiredBool(element, "requires_review"));
    }

    private static string? PolicyIdentity(JsonElement? origin)
    {
        if (origin is null)
        {
            return null;
        }

        JsonElement value = origin.Value;
        string? sourcePath = OptionalString(value, "source_path");
        string? yamlPath = OptionalString(value, "yaml_path");
        return sourcePath is null && yamlPath is null
            ? null
            : $"{sourcePath ?? string.Empty}:{yamlPath ?? string.Empty}";
    }

    internal static ArchitecturePrReportDebtGateReceipt ReadDebtGate(JsonElement element)
    {
        JsonElement evaluation = Required(element, "evaluation", JsonValueKind.Object);
        JsonElement persistentDebt = Required(element, "persistent_debt", JsonValueKind.Object);
        JsonElement policyWeakening = Required(element, "policy_weakening", JsonValueKind.Object);
        bool succeeded = RequiredBool(element, "succeeded");
        bool passed = RequiredBool(element, "passed");
        var parsedEvaluation = new ArchitecturePrReportDebtEvaluation(
                RequiredBool(evaluation, "completed"),
                RequiredString(evaluation, "mode"),
                RequiredBool(evaluation, "reused_analysis_snapshot"),
                Required(evaluation, "preflight_diagnostics", JsonValueKind.Array).EnumerateArray()
                    .Select(ReadFinding).ToArray());
        ArchitecturePrReportPolicyWeakening? parsedWeakening = ReadPolicyWeakening(policyWeakening);
        if (RequiredBool(policyWeakening, "requested") && (!succeeded || !parsedEvaluation.Completed || parsedWeakening is null))
        {
            throw InvalidArtifact("Requested policy-weakening evidence must be complete before it can be reported.");
        }

        return new ArchitecturePrReportDebtGateReceipt(
            succeeded,
            passed,
            parsedEvaluation,
            ReadPersistentDebt(persistentDebt),
            parsedWeakening);
    }

    private static ArchitecturePrReportPersistentDebt ReadPersistentDebt(JsonElement element) =>
        new(
            RequiredBool(element, "succeeded"),
            RequiredBool(element, "in_sync"),
            Required(element, "entries", JsonValueKind.Array).EnumerateArray()
                .Select(ReadBaselineEntry).ToArray(),
            Required(element, "configuration_violations", JsonValueKind.Array).EnumerateArray()
                .Select(ReadFinding).ToArray());

    private static ArchitecturePrReportBaselineEntry ReadBaselineEntry(JsonElement element)
    {
        string status = RequiredString(element, "status");
        if (!BaselineEntryLifecycleNames.All.Contains(status, StringComparer.Ordinal))
        {
            throw InvalidArtifact($"Unsupported baseline lifecycle status '{status}'.");
        }

        string disposition = RequiredString(element, "disposition");
        if (disposition is not (BaselineEntryDispositionNames.Reported
            or BaselineEntryDispositionNames.Added
            or BaselineEntryDispositionNames.Retained
            or BaselineEntryDispositionNames.Removed))
        {
            throw InvalidArtifact($"Unsupported baseline entry disposition '{disposition}'.");
        }

        return new ArchitecturePrReportBaselineEntry(
            status,
            disposition,
            RequiredString(element, "contract_group"),
            RequiredString(element, "contract_id"),
            RequiredString(element, "source_type"),
            RequiredString(element, "forbidden_reference"),
            OptionalString(element, "reason"),
            OptionalString(element, "issue"),
            OptionalString(element, "current_forbidden_reference"),
            OptionalString(element, "identity"));
    }

    private static ArchitecturePrReportPolicyWeakening? ReadPolicyWeakening(JsonElement element)
    {
        if (!RequiredBool(element, "requested"))
        {
            return null;
        }

        return new ArchitecturePrReportPolicyWeakening(
            RequiredInt(element, "schema_version"),
            RequiredString(element, "kind"),
            RequiredString(element, "policy_name"),
            RequiredInt(element, "policy_version"),
            RequiredString(element, "severity"),
            RequiredBool(element, "has_blocking_findings"),
            Required(element, "findings", JsonValueKind.Array).EnumerateArray()
                .Select(ReadPolicyWeakeningFinding).ToArray());
    }

    private static ArchitecturePrReportPolicyWeakeningFinding ReadPolicyWeakeningFinding(JsonElement element) =>
        new(
            RequiredString(element, "identity"),
            RequiredString(element, "kind"),
            RequiredString(element, "control_identity"),
            RequiredString(element, "classification"),
            RequiredString(element, "severity"),
            ReadStringArray(Required(element, "base_values", JsonValueKind.Array)),
            ReadStringArray(Required(element, "current_values", JsonValueKind.Array)),
            ReadStringArray(Required(element, "affected_subjects", JsonValueKind.Array)),
            ReadPolicyContextProvenance(element, "base_provenance"),
            ReadPolicyContextProvenance(element, "current_provenance"),
            OptionalString(element, "rationale"));

    private static ArchitecturePrReportPolicyContextProvenance? ReadPolicyContextProvenance(
        JsonElement parent,
        string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new ArchitecturePrReportPolicyContextProvenance(
            RequiredString(element, "source_path"),
            RequiredString(element, "root_path"),
            RequiredString(element, "role"),
            RequiredString(element, "yaml_path"),
            RequiredInt(element, "source_order"));
    }

    internal static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidArtifact("The report artifact requires an array of strings.");
        }

        return element.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()!
                : throw InvalidArtifact("The report artifact contains a non-string array value."))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("The report artifact requires a string map.");
        }

        return element.EnumerateObject()
            .ToDictionary(item => item.Name, item => RequiredString(item.Value), StringComparer.Ordinal);
    }

    internal static DateOnly? OptionalDate(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null
            : ParseDate(value, name);

    internal static DateOnly RequiredDate(JsonElement parent, string name) =>
        ParseDate(Required(parent, name, JsonValueKind.String), name)
        ?? throw InvalidArtifact($"The report artifact requires a date for '{name}'.");

    private static DateOnly? ParseDate(JsonElement value, string name)
    {
        string? text = value.GetString();
        return text is null
            ? null
            : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date)
                ? date
                : throw InvalidArtifact($"The report artifact field '{name}' contains an invalid date.");
    }

    internal static bool RequiredBool(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.True, JsonValueKind.False);
        return value.GetBoolean();
    }

    private static JsonElement Required(JsonElement parent, string name, JsonValueKind first, JsonValueKind? second = null)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)
            || (value.ValueKind != first && (second is null || value.ValueKind != second.Value)))
        {
            throw InvalidArtifact($"The report artifact requires '{name}'.");
        }

        return value;
    }

    internal static int? OptionalInt(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
                ? number
                : throw InvalidArtifact($"The report artifact field '{name}' must be an integer or null.");
}
