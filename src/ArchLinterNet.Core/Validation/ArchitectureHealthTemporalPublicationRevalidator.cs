using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

/// <summary>Binding values supplied by the already-authorized publication provider.</summary>
internal sealed record ArchitectureHealthTemporalPublicationBinding(
    string SourceHealthSha256,
    string BadgePayloadSha256,
    string MergedTreeSha,
    string ProducerIdentitySha256);

/// <summary>
/// Revalidates only the finite temporal facts in a serialized Health report-evidence envelope.
/// This type intentionally has no policy, build, assembly, or analysis inputs.
/// </summary>
internal static class ArchitectureHealthTemporalPublicationRevalidator
{
    private const string MissingEvidence = "missing_report_evidence";
    private const string MalformedEvidence = "malformed_report_evidence";
    private const string InvalidBinding = "invalid_publication_binding";
    private const string SourceDigestMismatch = "source_health_sha256_mismatch";
    private const string MissingPolicyInventory = "missing_policy_inventory";
    private const string InvalidPolicyInventory = "invalid_policy_inventory";
    private const string MissingWaiverReceipt = "missing_waiver_receipt";
    private const string MalformedWaiverReceipt = "malformed_waiver_receipt";
    private const string InvalidWaiver = "invalid_waiver";
    private const string StaleWaiver = "stale_waiver";
    private const string MetadataIncompleteWaiver = "metadata_incomplete_waiver";
    private const string ExpiredWaiver = "expired_waiver";
    private const string InconsistentWaiverReceipt = "inconsistent_waiver_receipt";
    private const string InconsistentEvaluationDate = "inconsistent_evaluation_date";
    private const string InvalidEvaluationDate = "invalid_evaluation_date";
    private const string MissingExternalEvidenceReceipt = "missing_external_evidence_receipt";
    private const string InvalidExternalEvidence = "invalid_external_evidence";
    private const string StaleExternalEvidence = "stale_external_evidence";
    private const string RequiredExternalEvidenceHorizonUnknown = "required_external_evidence_horizon_unknown";
    private const string OriginalPublicationEvidenceUnassessable = "original_publication_evidence_unassessable";

    internal static ArchitectureHealthTemporalPublicationReceipt Revalidate(
        ReadOnlySpan<byte> healthBytes,
        DateOnly evaluationDate,
        ArchitectureHealthTemporalPublicationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        string actualSourceDigest = Convert.ToHexStringLower(SHA256.HashData(healthBytes));
        var reasons = new List<ArchitectureHealthTemporalPublicationReceiptReason>();
        string sourceDigest = NormalizeDigest(binding.SourceHealthSha256);
        string payloadDigest = NormalizeDigest(binding.BadgePayloadSha256);
        string treeSha = NormalizeDigest(binding.MergedTreeSha);
        string producerDigest = NormalizeDigest(binding.ProducerIdentitySha256);

        ValidateBindings(binding, actualSourceDigest, reasons);
        if (reasons.Count > 0)
        {
            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }

        if (!string.Equals(sourceDigest, actualSourceDigest, StringComparison.Ordinal))
        {
            reasons.Add(new(SourceDigestMismatch,
                "The supplied source Health digest does not match the exact input bytes."));
            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }

        ArchitecturePrReportEvidence evidence;
        bool originalPublicationReady;
        try
        {
            originalPublicationReady = ValidatePublicationEvidenceEnvelope(healthBytes);
            string json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(healthBytes);
            evidence = ArchitecturePrReportReader.ReadHealthReportEvidence(json);
        }
        catch (DecoderFallbackException exception)
        {
            reasons.Add(new(MalformedEvidence, exception.Message));
            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }
        catch (ArgumentException exception)
        {
            reasons.Add(new(MalformedEvidence, exception.Message));
            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }
        catch (JsonException exception)
        {
            reasons.Add(new(MalformedEvidence, exception.Message));
            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }

        if (!originalPublicationReady)
        {
            reasons.Add(new(OriginalPublicationEvidenceUnassessable,
                "The original publication evidence was already unassessable and cannot be revived."));
        }

        DateTimeOffset? horizon = null;
        DateOnly? sourceEvaluationDate = null;
        foreach (ArchitecturePrReportValidationReceipt receipt in evidence.ValidationOutcomes
            .OrderBy(item => item.Mode, StringComparer.Ordinal))
        {
            ProcessReceipt(receipt, evaluationDate, ref sourceEvaluationDate, ref horizon, reasons);
        }

        if (sourceEvaluationDate is null)
        {
            reasons.Add(new(InvalidEvaluationDate, "Temporal evidence must carry an explicit source evaluation date."));
        }

        if (reasons.Count > 0 || horizon is null)
        {
            if (horizon is null && reasons.Count == 0)
            {
                reasons.Add(new(InvalidEvaluationDate, "Temporal evidence cannot produce a finite horizon."));
            }

            return Unassessable(evaluationDate, actualSourceDigest, payloadDigest, treeSha, producerDigest, reasons);
        }

        return new ArchitectureHealthTemporalPublicationReceipt(
            ArchitectureHealthTemporalPublicationReceiptState.Ready,
            evaluationDate,
            horizon,
            actualSourceDigest,
            payloadDigest,
            treeSha,
            producerDigest,
            Array.Empty<ArchitectureHealthTemporalPublicationReceiptReason>());
    }

    private static void ValidateBindings(
        ArchitectureHealthTemporalPublicationBinding binding,
        string actualSourceDigest,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons)
    {
        if (!IsSha256(binding.SourceHealthSha256)
            || !IsSha256(binding.BadgePayloadSha256)
            || !IsCommitSha(binding.MergedTreeSha)
            || !IsSha256(binding.ProducerIdentitySha256))
        {
            reasons.Add(new(InvalidBinding,
                "Source Health, payload, producer identity, and merged-tree bindings must be canonical hexadecimal digests."));
        }

        if (string.IsNullOrWhiteSpace(actualSourceDigest))
        {
            reasons.Add(new(InvalidBinding, "The source Health digest could not be computed."));
        }
    }

    private static bool ValidatePublicationEvidenceEnvelope(ReadOnlySpan<byte> healthBytes)
    {
        using JsonDocument document = JsonDocument.Parse(healthBytes.ToArray());
        JsonElement root = document.RootElement;
        JsonElement reportEvidence = Required(root, "report_evidence", JsonValueKind.Object);
        if (RequiredInt(reportEvidence, "schema_version") != ArchitecturePrReportEvidence.CurrentSchemaVersion
            || !string.Equals(RequiredString(reportEvidence, "kind"), ArchitecturePrReportEvidence.EvidenceKind, StringComparison.Ordinal))
        {
            throw new ArgumentException("The report-evidence envelope has an unsupported schema or kind.");
        }

        JsonElement publication = Required(reportEvidence, "publication_evidence", JsonValueKind.Object);
        if (!string.Equals(RequiredString(publication, "schema_id"), ArchitectureHealthPublicationEvidence.CurrentSchemaId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The report-evidence envelope has an unsupported publication-evidence schema.");
        }

        string state = RequiredString(publication, "state");
        if (state is not ("ready" or "unassessable"))
        {
            throw new ArgumentException("The publication-evidence state is unsupported.");
        }

        JsonElement horizon = Required(publication, "semantic_horizon");
        if (state == "ready"
            && (horizon.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParseExact(
                    horizon.GetString(),
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out _)))
        {
            throw new ArgumentException("The ready publication-evidence semantic horizon must be a UTC timestamp.");
        }
        if (state == "unassessable" && horizon.ValueKind != JsonValueKind.Null)
        {
            throw new ArgumentException("Unassessable publication evidence must not contain a semantic horizon.");
        }

        JsonElement reasons = Required(publication, "reasons", JsonValueKind.Array);
        if (state == "ready" && reasons.GetArrayLength() != 0)
        {
            throw new ArgumentException("Ready publication evidence must not contain reasons.");
        }
        if (state == "unassessable" && reasons.GetArrayLength() == 0)
        {
            throw new ArgumentException("Unassessable publication evidence must contain a reason.");
        }
        foreach (JsonElement reason in reasons.EnumerateArray())
        {
            RequireObject(reason, "A publication-evidence reason");
            RequiredString(reason, "code");
            RequiredString(reason, "detail");
        }

        return state == "ready";
    }

    private static void ProcessReceipt(
        ArchitecturePrReportValidationReceipt receipt,
        DateOnly evaluationDate,
        ref DateOnly? sourceEvaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(receipt.Mode))
        {
            reasons.Add(new(MalformedEvidence, "A validation receipt must identify its evaluation mode."));
        }

        ArchitecturePolicyInventory? inventory = receipt.PolicyInventory;
        if (inventory is null)
        {
            reasons.Add(new(MissingPolicyInventory, $"The '{receipt.Mode}' validation receipt has no policy inventory."));
        }
        else if (!IsValidInventory(inventory))
        {
            reasons.Add(new(InvalidPolicyInventory, $"The '{receipt.Mode}' policy inventory is not a trusted v1 receipt."));
        }

        ArchitectureWaiverLifecycleAssessment? lifecycle = receipt.WaiverLifecycle;
        if (lifecycle is null)
        {
            reasons.Add(new(MissingWaiverReceipt, $"The '{receipt.Mode}' validation receipt has no waiver lifecycle receipt."));
        }
        else
        {
            ProcessLifecycle(receipt.Mode, inventory, lifecycle, evaluationDate,
                ref sourceEvaluationDate, ref horizon, reasons);
        }

        ProcessExternalEvidence(receipt.Mode, receipt.ExternalEvidence, reasons);
    }

    private static void ProcessLifecycle(
        string mode,
        ArchitecturePolicyInventory? inventory,
        ArchitectureWaiverLifecycleAssessment lifecycle,
        DateOnly evaluationDate,
        ref DateOnly? sourceEvaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(lifecycle.Profile))
        {
            reasons.Add(new(MalformedWaiverReceipt, $"The '{mode}' waiver lifecycle receipt has no profile."));
        }

        if (lifecycle.EvaluationDate is { } assessmentDate)
        {
            SetSourceEvaluationDate(assessmentDate, ref sourceEvaluationDate, reasons);
        }

        ArchitectureWaiverLifecycleRecord[] records = lifecycle.Records.ToArray();
        if (records.Length == 0 && lifecycle.EvaluationDate is null)
        {
            reasons.Add(new(InvalidEvaluationDate, $"The '{mode}' waiver lifecycle receipt has no source evaluation date."));
        }

        if (inventory is not null && !WaiverSetsEqual(inventory.Waivers, records))
        {
            reasons.Add(new(InconsistentWaiverReceipt, $"The '{mode}' policy inventory and waiver lifecycle receipts differ."));
        }

        if (!TryGetNextUtcDay(evaluationDate, out DateTimeOffset evaluationHorizon))
        {
            reasons.Add(new(InvalidEvaluationDate, "The supplied UTC evaluation date cannot produce a finite horizon."));
            return;
        }

        horizon = horizon is null || evaluationHorizon < horizon.Value ? evaluationHorizon : horizon;
        foreach (ArchitectureWaiverLifecycleRecord record in records)
        {
            ProcessWaiver(record, evaluationDate, reasons, ref horizon);
        }
    }

    private static void ProcessWaiver(
        ArchitectureWaiverLifecycleRecord record,
        DateOnly evaluationDate,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons,
        ref DateTimeOffset? horizon)
    {
        if (string.IsNullOrWhiteSpace(record.Id)
            || string.IsNullOrWhiteSpace(record.State)
            || record.EvaluationDate == DateOnly.MinValue)
        {
            reasons.Add(new(MalformedWaiverReceipt, "The waiver lifecycle receipt contains an incomplete record."));
            return;
        }

        if (record.EvaluationDate != evaluationDate && record.EvaluationDate > evaluationDate)
        {
            reasons.Add(new(InconsistentEvaluationDate,
                $"The waiver '{record.Id}' was evaluated after the supplied UTC evaluation date."));
        }

        switch (record.State)
        {
            case "active":
                break;
            case "expired":
                reasons.Add(new(ExpiredWaiver, $"The waiver '{record.Id}' expired before the supplied evaluation date."));
                break;
            case "stale":
                reasons.Add(new(StaleWaiver, $"The waiver '{record.Id}' is stale and cannot be reused."));
                break;
            case "invalid":
                reasons.Add(new(InvalidWaiver, $"The waiver '{record.Id}' is invalid and cannot be reused."));
                break;
            case "metadata_incomplete":
                reasons.Add(new(MetadataIncompleteWaiver, $"The waiver '{record.Id}' has incomplete lifecycle metadata."));
                break;
            default:
                reasons.Add(new(MalformedWaiverReceipt, $"The waiver '{record.Id}' has an unsupported lifecycle state."));
                break;
        }

        if (record.Expires is { } expiry)
        {
            if (expiry < evaluationDate || record.State == "expired")
            {
                reasons.Add(new(ExpiredWaiver, $"The waiver '{record.Id}' expired before the supplied evaluation date."));
            }
            else if (!TryGetNextUtcDay(expiry, out DateTimeOffset expiryHorizon))
            {
                reasons.Add(new(InvalidEvaluationDate, $"The waiver '{record.Id}' expiry cannot produce a finite horizon."));
            }
            else if (horizon is null || expiryHorizon < horizon.Value)
            {
                horizon = expiryHorizon;
            }
        }
    }

    private static void ProcessExternalEvidence(
        string mode,
        ArchitecturePrReportExternalEvidence? external,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons)
    {
        if (external is null)
        {
            return;
        }

        foreach (ArchitecturePrReportExternalRequirement requirement in external.Requirements
            .Where(item => item.Required)
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            ArchitecturePrReportExternalEvidenceTrustReceipt? receipt = external.TrustReceipts
                .SingleOrDefault(item => string.Equals(item.LogicalId, requirement.Id, StringComparison.Ordinal));
            if (receipt is null)
            {
                reasons.Add(new(MissingExternalEvidenceReceipt,
                    $"Required external evidence '{requirement.Id}' has no trust receipt."));
            }
            else if (receipt.State == ArchitecturePrReportExternalEvidenceTrustState.Stale)
            {
                reasons.Add(new(StaleExternalEvidence,
                    $"Required external evidence '{requirement.Id}' is stale."));
            }
            else if (receipt.State != ArchitecturePrReportExternalEvidenceTrustState.Current)
            {
                reasons.Add(new(InvalidExternalEvidence,
                    $"Required external evidence '{requirement.Id}' is not current."));
            }

            reasons.Add(new(RequiredExternalEvidenceHorizonUnknown,
                $"Required external evidence '{requirement.Id}' has no finite reuse horizon."));
        }
    }

    private static void SetSourceEvaluationDate(
        DateOnly value,
        ref DateOnly? sourceEvaluationDate,
        List<ArchitectureHealthTemporalPublicationReceiptReason> reasons)
    {
        if (value == DateOnly.MinValue)
        {
            reasons.Add(new(InvalidEvaluationDate, "The source waiver evaluation date is invalid."));
            return;
        }

        if (sourceEvaluationDate is null)
        {
            sourceEvaluationDate = value;
        }
        else if (sourceEvaluationDate.Value != value)
        {
            reasons.Add(new(InconsistentEvaluationDate, "Waiver lifecycle receipts do not share one source evaluation date."));
        }
    }

    private static bool IsValidInventory(ArchitecturePolicyInventory inventory) =>
        string.Equals(inventory.SchemaId, ArchitecturePolicyInventory.CurrentSchemaId, StringComparison.Ordinal)
        && inventory.EffectiveRuleCount >= 0
        && inventory.Rules is not null
        && inventory.IgnoreDebt is not null
        && inventory.Waivers is not null
        && inventory.Rules.Strict >= 0
        && inventory.Rules.Audit >= 0
        && inventory.Rules.Coverage >= 0
        && inventory.IgnoreDebt.Total >= 0;

    private static bool WaiverSetsEqual(
        IReadOnlyList<ArchitectureWaiverLifecycleRecord> left,
        IReadOnlyList<ArchitectureWaiverLifecycleRecord> right) =>
        left.Select(WaiverKey).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(right.Select(WaiverKey).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);

    private static string WaiverKey(ArchitectureWaiverLifecycleRecord record) => string.Join('\u001F',
        record.Id,
        record.State,
        record.ContractName,
        record.ContractId,
        record.ContractGroup,
        record.SourceType,
        record.ForbiddenReference,
        record.TargetFingerprint,
        record.Reason,
        record.Owner,
        record.Issue,
        FormatDate(record.Introduced),
        FormatDate(record.Expires),
        FormatDate(record.EvaluationDate),
        record.MatchesGovernedFinding.ToString(CultureInfo.InvariantCulture));

    private static ArchitectureHealthTemporalPublicationReceipt Unassessable(
        DateOnly evaluationDate,
        string sourceHealthSha256,
        string badgePayloadSha256,
        string mergedTreeSha,
        string producerIdentitySha256,
        IEnumerable<ArchitectureHealthTemporalPublicationReceiptReason> reasons) =>
        new(
            ArchitectureHealthTemporalPublicationReceiptState.Unassessable,
            evaluationDate,
            null,
            sourceHealthSha256,
            badgePayloadSha256,
            mergedTreeSha,
            producerIdentitySha256,
            reasons
                .GroupBy(reason => (reason.Code, reason.Detail))
                .Select(group => group.First())
                .ToArray());

    private static string NormalizeDigest(string value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string? value) => value is not null
        && value.Trim().Length == 64
        && value.Trim().All(char.IsAsciiHexDigit);

    private static bool IsCommitSha(string? value) => value is not null
        && value.Trim().Length == 40
        && value.Trim().All(char.IsAsciiHexDigit);

    private static bool TryGetNextUtcDay(DateOnly date, out DateTimeOffset value)
    {
        if (date >= DateOnly.MaxValue)
        {
            value = default;
            return false;
        }

        value = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return true;
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static JsonElement Required(JsonElement parent, string name, JsonValueKind? kind = null)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(name, out JsonElement value)
            || (kind is not null && value.ValueKind != kind.Value))
        {
            throw new ArgumentException($"The Health report artifact requires '{name}'.");
        }

        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.String);
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException($"The Health report artifact requires a non-empty '{name}'.")
            : text;
    }

    private static int RequiredInt(JsonElement parent, string name)
    {
        JsonElement value = Required(parent, name, JsonValueKind.Number);
        return value.TryGetInt32(out int result)
            ? result
            : throw new ArgumentException($"The Health report artifact field '{name}' must be an integer.");
    }

    private static void RequireObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{description} must be a JSON object.");
        }
    }
}
