using System.Text.Json;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Reads the two canonical, local artifacts consumed by an architecture PR report.</summary>
public static partial class ArchitecturePrReportReader
{
    /// <summary>
    /// Parses one architecture-health/v1 artifact and one versioned architecture-change report.
    /// Analysis, policy loading, and lifecycle evaluation are deliberately outside this boundary.
    /// </summary>
    public static ArchitecturePrReportInput Read(string healthJson, string changeJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(healthJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeJson);
        try
        {
            using JsonDocument healthDocument = JsonDocument.Parse(healthJson);
            ArchitecturePrReportInput health = ReadHealth(healthDocument.RootElement);
            ArchitectureChangeReport change = ArchitectureChangeReports.DeserializeReport(changeJson);
            return health with { Change = change };
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The architecture PR report input contains malformed JSON.", exception);
        }
    }

    /// <summary>Alias for callers that use deserialize terminology for persisted artifacts.</summary>
    public static ArchitecturePrReportInput Deserialize(string healthJson, string changeJson) =>
        Read(healthJson, changeJson);

    private static ArchitecturePrReportInput ReadHealth(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("The architecture Health artifact must be a JSON object.");
        }

        string schemaId = RequiredString(root, "schema_id");
        if (!string.Equals(schemaId, ArchitectureHealthSummary.CurrentSchemaId, StringComparison.Ordinal))
        {
            throw InvalidArtifact($"Unsupported architecture Health schema '{schemaId}'.");
        }

        ArchitectureHealthGate gate = ParseGate(RequiredString(root, "gate"));
        ArchitectureHealthState health = ParseHealth(RequiredString(root, "health"));
        JsonElement dimensions = Required(root, "dimensions", JsonValueKind.Array);
        ArchitectureHealthDimension[] parsedDimensions = dimensions.EnumerateArray()
            .Select(ReadDimension)
            .OrderBy(dimension => dimension.Name, StringComparer.Ordinal)
            .ToArray();
        var summary = new ArchitectureHealthSummary(schemaId, gate, health, parsedDimensions);

        ArchitecturePrReportEvidence? evidence = root.TryGetProperty("report_evidence", out JsonElement evidenceElement)
            ? ReadEvidence(evidenceElement, gate, health)
            : null;
        return new ArchitecturePrReportInput(summary, evidence, null!);
    }

    private static ArchitecturePrReportEvidence ReadEvidence(
        JsonElement element,
        ArchitectureHealthGate summaryGate,
        ArchitectureHealthState summaryHealth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("The report_evidence envelope must be a JSON object.");
        }

        int schemaVersion = RequiredInt(element, "schema_version");
        string kind = RequiredString(element, "kind");
        if (schemaVersion != ArchitecturePrReportEvidence.CurrentSchemaVersion
            || !string.Equals(kind, ArchitecturePrReportEvidence.EvidenceKind, StringComparison.Ordinal))
        {
            throw InvalidArtifact("Unsupported architecture Health report-evidence version or kind.");
        }

        ArchitectureHealthGate gate = ParseGate(RequiredString(element, "gate"));
        ArchitectureHealthState health = ParseHealth(RequiredString(element, "health"));
        if (gate != summaryGate || health != summaryHealth)
        {
            throw InvalidArtifact("Health summary and report-evidence gate/health do not match.");
        }

        JsonElement receipts = Required(element, "validation_outcomes", JsonValueKind.Array);
        ArchitecturePrReportValidationReceipt[] parsedReceipts = receipts.EnumerateArray()
            .Select(ReadValidationReceipt)
            .OrderBy(receipt => receipt.Mode, StringComparer.Ordinal)
            .ToArray();
        if (parsedReceipts.Length == 0)
        {
            throw InvalidArtifact("The report-evidence envelope must contain a validation receipt.");
        }

        ArchitecturePrReportDebtGateReceipt debtGate = ReadDebtGate(
            Required(element, "debt_gate", JsonValueKind.Object));
        return new ArchitecturePrReportEvidence(
            schemaVersion,
            kind,
            gate,
            health,
            parsedReceipts,
            debtGate);
    }

    private static ArchitecturePrReportValidationReceipt ReadValidationReceipt(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("A report-evidence validation receipt must be a JSON object.");
        }

        string mode = RequiredString(element, "mode");
        if (mode is not ("strict" or "audit"))
        {
            throw InvalidArtifact($"Unsupported validation receipt mode '{mode}'.");
        }

        JsonElement availability = Required(element, "availability", JsonValueKind.Object);
        Dictionary<string, string> availabilityValues = availability.EnumerateObject()
            .ToDictionary(property => property.Name, property => RequiredString(property.Value), StringComparer.Ordinal);
        ArchitecturePolicyInventory? inventory = element.TryGetProperty("policy_inventory", out JsonElement inventoryElement)
            ? ReadPolicyInventory(inventoryElement)
            : null;
        ArchitectureWaiverLifecycleAssessment? lifecycle = element.TryGetProperty("waiver_lifecycle", out JsonElement lifecycleElement)
            ? ReadWaiverLifecycle(lifecycleElement)
            : null;
        ArchitecturePrReportApplicability? applicability = element.TryGetProperty("applicability", out JsonElement applicabilityElement)
            ? ReadApplicability(applicabilityElement)
            : null;
        ArchitecturePrReportExternalEvidence? external = element.TryGetProperty("external_evidence", out JsonElement externalElement)
            ? ReadExternalEvidence(externalElement)
            : null;
        JsonElement findings = Required(element, "findings", JsonValueKind.Array);
        ArchitecturePrReportFinding[] parsedFindings = findings.EnumerateArray()
            .Select(ReadFinding)
            .OrderBy(finding => finding.ContractId ?? finding.ContractName, StringComparer.Ordinal)
            .ThenBy(finding => finding.CanonicalIdentity, StringComparer.Ordinal)
            .ThenBy(finding => finding.Kind, StringComparer.Ordinal)
            .ToArray();
        ArchitecturePrReportProvenance provenance = ReadProvenance(
            Required(element, "provenance", JsonValueKind.Object));
        return new ArchitecturePrReportValidationReceipt(
            mode,
            availabilityValues,
            inventory,
            lifecycle,
            applicability,
            external,
            parsedFindings,
            provenance);
    }

    private static ArchitectureHealthDimension ReadDimension(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("A Health dimension must be a JSON object.");
        }

        string name = RequiredString(element, "name");
        ArchitectureHealthDimensionState state = ParseDimensionState(RequiredString(element, "state"));
        JsonElement reasons = Required(element, "reasons", JsonValueKind.Array);
        ArchitectureHealthReason[] parsedReasons = reasons.EnumerateArray()
            .Select(ReadReason)
            .OrderBy(reason => reason.Code, StringComparer.Ordinal)
            .ThenBy(reason => reason.Source, StringComparer.Ordinal)
            .ToArray();
        return new ArchitectureHealthDimension(name, state, parsedReasons);
    }

    private static ArchitectureHealthReason ReadReason(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidArtifact("A Health reason must be a JSON object.");
        }

        return new ArchitectureHealthReason(
            RequiredString(element, "code"),
            RequiredString(element, "source"))
        {
            Family = OptionalString(element, "family"),
            ControlIdentity = OptionalString(element, "control_identity"),
            PolicyIdentity = OptionalString(element, "policy_identity"),
            EvidenceIdentity = OptionalString(element, "evidence_identity"),
        };
    }

    private static ArchitectureHealthGate ParseGate(string value) => value switch
    {
        "pass" => ArchitectureHealthGate.Pass,
        "fail" => ArchitectureHealthGate.Fail,
        "unassessable" => ArchitectureHealthGate.Unassessable,
        _ => throw InvalidArtifact($"Unsupported Health gate '{value}'."),
    };

    private static ArchitectureHealthState ParseHealth(string value) => value switch
    {
        "healthy" => ArchitectureHealthState.Healthy,
        "debt" => ArchitectureHealthState.Debt,
        "degrading" => ArchitectureHealthState.Degrading,
        "failing" => ArchitectureHealthState.Failing,
        "unassessable" => ArchitectureHealthState.Unassessable,
        _ => throw InvalidArtifact($"Unsupported Health state '{value}'."),
    };

    private static ArchitectureHealthDimensionState ParseDimensionState(string value) => value switch
    {
        "pass" => ArchitectureHealthDimensionState.Pass,
        "fail" => ArchitectureHealthDimensionState.Fail,
        "debt" => ArchitectureHealthDimensionState.Debt,
        "degrading" => ArchitectureHealthDimensionState.Degrading,
        "unassessable" => ArchitectureHealthDimensionState.Unassessable,
        "not_configured" => ArchitectureHealthDimensionState.NotConfigured,
        "not_applicable" => ArchitectureHealthDimensionState.NotApplicable,
        _ => throw InvalidArtifact($"Unsupported Health dimension state '{value}'."),
    };

    private static JsonElement Required(JsonElement parent, string name, JsonValueKind kind)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != kind)
        {
            throw InvalidArtifact($"The Health report artifact requires '{name}' as {kind}.");
        }

        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.String);
        return RequiredString(value);
    }

    private static string RequiredString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw InvalidArtifact("The Health report artifact requires a string value.");
        }

        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text)
            ? throw InvalidArtifact("The Health report artifact requires a non-empty string.")
            : text;
    }

    private static string? OptionalString(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw InvalidArtifact($"The Health report artifact field '{name}' must be a string or null.");

    private static int RequiredInt(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.Number);
        return value.TryGetInt32(out int result)
            ? result
            : throw InvalidArtifact($"The Health report artifact field '{name}' must be a 32-bit integer.");
    }

    private static ArgumentException InvalidArtifact(string message) =>
        new(message, "json");
}
