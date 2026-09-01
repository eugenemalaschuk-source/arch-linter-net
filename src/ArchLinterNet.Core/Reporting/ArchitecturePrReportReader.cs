using System.Text.Json;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitecturePrReportDebtReceiptParser;
using static ArchLinterNet.Core.Reporting.ArchitecturePrReportReceiptParser;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Reads the two canonical, local artifacts consumed by an architecture PR report.</summary>
public static class ArchitecturePrReportReader
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
            ArchitecturePrReportChange reportChange = ReadChange(change);
            ValidateCompatibleContext(health.Evidence, reportChange);
            return health with { Change = reportChange };
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

        if (parsedReceipts.Select(receipt => receipt.Mode).Distinct(StringComparer.Ordinal).Count() != parsedReceipts.Length)
        {
            throw InvalidArtifact("The report-evidence envelope must not repeat a validation receipt mode.");
        }

        ArchitecturePrReportDebtGateReceipt debtGate = ReadDebtGate(
            Required(element, "debt_gate", JsonValueKind.Object));
        return new ArchitecturePrReportEvidence(
            schemaVersion,
            kind,
            gate,
            health,
            parsedReceipts,
            debtGate)
        {
            ExecutionContext = ReadExecutionContext(Required(element, "execution_context", JsonValueKind.Object)),
        };
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
        Dictionary<string, string> availabilityValues = ReadAvailability(
            availability,
            inventory,
            lifecycle,
            applicability,
            external,
            findings);
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

    private static ArchitecturePrReportExecutionContext ReadExecutionContext(JsonElement element)
    {
        string executionId = RequiredString(element, "execution_id");
        JsonElement conditionSet = Required(element, "condition_set", JsonValueKind.String);
        return new ArchitecturePrReportExecutionContext(executionId, conditionSet.GetString() ?? string.Empty);
    }

    private static Dictionary<string, string> ReadAvailability(
        JsonElement availability,
        ArchitecturePolicyInventory? inventory,
        ArchitectureWaiverLifecycleAssessment? lifecycle,
        ArchitecturePrReportApplicability? applicability,
        ArchitecturePrReportExternalEvidence? external,
        JsonElement findings)
    {
        string[] expectedKeys =
        [
            "applicability",
            "external_evidence",
            "findings",
            "policy_inventory",
            "topology",
            "waiver_lifecycle",
        ];
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (JsonProperty property in availability.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, RequiredString(property.Value)))
            {
                throw InvalidArtifact($"The availability map repeats '{property.Name}'.");
            }
        }

        if (values.Count != expectedKeys.Length || !expectedKeys.All(values.ContainsKey))
        {
            throw InvalidArtifact("The availability map must contain exactly the supported authority keys.");
        }

        ValidateAvailabilityValue(values, "policy_inventory", inventory is not null, "available", "unavailable");
        ValidateAvailabilityValue(values, "waiver_lifecycle", lifecycle is not null, "available", "unavailable");
        ValidateAvailabilityValue(values, "applicability", applicability is not null, "available", "unavailable");
        bool hasTopology = applicability?.Controls.Any(control => control.Record?.Topology is not null) == true;
        ValidateAvailabilityValue(values, "topology", hasTopology, "available", "not_configured");
        ValidateAvailabilityValue(values, "external_evidence", external is not null, "available", "not_configured");
        ValidateAvailabilityValue(values, "findings", findings.ValueKind == JsonValueKind.Array, "available", "unavailable");
        return values;
    }

    private static void ValidateAvailabilityValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool hasPayload,
        string available,
        string absent)
    {
        string value = values[key];
        if (!string.Equals(value, available, StringComparison.Ordinal)
            && !string.Equals(value, absent, StringComparison.Ordinal))
        {
            throw InvalidArtifact($"Unsupported availability value '{value}' for '{key}'.");
        }

        if (hasPayload != string.Equals(value, available, StringComparison.Ordinal))
        {
            throw InvalidArtifact($"Availability '{key}={value}' does not match its canonical payload.");
        }
    }

    private static ArchitecturePrReportChange ReadChange(ArchitectureChangeReport change)
    {
        ArchitectureChangeReportContext context = change.ExecutionContext
            ?? throw InvalidArtifact("The architecture change report requires execution context.");
        return new ArchitecturePrReportChange(
            new ArchitecturePrReportExecutionContext(context.ExecutionId, context.ConditionSet),
            context.Mode,
            change.Added,
            change.Removed,
            change.NewFindings,
            change.ExistingFindings,
            change.ResolvedFindings,
            change.BaselineDebt);
    }

    private static void ValidateCompatibleContext(
        ArchitecturePrReportEvidence? evidence,
        ArchitecturePrReportChange change)
    {
        if (evidence is null)
        {
            return;
        }

        ArchitecturePrReportExecutionContext health = evidence.ExecutionContext
            ?? throw InvalidArtifact("The Health report evidence requires execution context.");
        if (!string.Equals(health.ExecutionId, change.ExecutionContext.ExecutionId, StringComparison.Ordinal)
            || !string.Equals(health.ConditionSetName, change.ExecutionContext.ConditionSetName, StringComparison.Ordinal))
        {
            throw InvalidArtifact("Health and change artifacts have incompatible execution context.");
        }

        int matchingReceipts = evidence.ValidationOutcomes.Count(receipt =>
            string.Equals(receipt.Mode, change.Mode, StringComparison.Ordinal));
        if (matchingReceipts != 1)
        {
            throw InvalidArtifact("Health report evidence must contain exactly one receipt for the change-report mode.");
        }
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

    internal static JsonElement Required(JsonElement parent, string name, JsonValueKind kind)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != kind)
        {
            throw InvalidArtifact($"The Health report artifact requires '{name}' as {kind}.");
        }

        return value;
    }

    internal static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.String);
        return RequiredString(value);
    }

    internal static string RequiredString(JsonElement value)
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

    internal static string? OptionalString(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw InvalidArtifact($"The Health report artifact field '{name}' must be a string or null.");

    internal static int RequiredInt(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.Number);
        return value.TryGetInt32(out int result)
            ? result
            : throw InvalidArtifact($"The Health report artifact field '{name}' must be a 32-bit integer.");
    }

    internal static ArgumentException InvalidArtifact(string message) =>
        new(message, "json");
}
