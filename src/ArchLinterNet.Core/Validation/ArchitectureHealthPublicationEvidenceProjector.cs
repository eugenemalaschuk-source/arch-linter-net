using System.Globalization;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Derives the finite semantic reuse horizon from canonical Health receipts. This projector is
/// deliberately clock-free: the only temporal facts it accepts are explicit evaluation and
/// structured-waiver dates already carried by the receipts.
/// </summary>
internal static class ArchitectureHealthPublicationEvidenceProjector
{
    private const string MissingValidationReceipt = "missing_validation_receipt";
    private const string MissingPolicyInventory = "missing_policy_inventory";
    private const string InvalidPolicyInventory = "invalid_policy_inventory";
    private const string MissingWaiverReceipt = "missing_waiver_receipt";
    private const string MissingEvaluationDate = "missing_evaluation_date";
    private const string InvalidEvaluationDate = "invalid_evaluation_date";
    private const string InconsistentEvaluationDate = "inconsistent_evaluation_date";
    private const string InconsistentWaiverReceipt = "inconsistent_waiver_receipt";
    private const string MalformedWaiverReceipt = "malformed_waiver_receipt";
    private const string ExpiredWaiver = "expired_waiver";
    private const string InconsistentExternalEvidenceReceipt = "inconsistent_external_evidence_receipt";
    private const string MissingExternalEvidenceReceipt = "missing_external_evidence_receipt";
    private const string RequiredExternalEvidenceHorizonUnknown = "required_external_evidence_horizon_unknown";

    internal static ArchitectureHealthPublicationEvidence Project(ArchitectureHealthOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        ArchitectureHealthValidationOutcome[] receipts = outcome.ValidationOutcomes is null
            ? Array.Empty<ArchitectureHealthValidationOutcome>()
            : outcome.ValidationOutcomes
                .Where(receipt => receipt is not null)
                .OrderBy(receipt => receipt.Mode, StringComparer.Ordinal)
                .ToArray();

        if (receipts.Length == 0)
        {
            reasons.Add(new(MissingValidationReceipt, "At least one complete validation receipt is required."));
            return Unassessable(reasons);
        }

        DateOnly? evaluationDate = null;
        DateTimeOffset? horizon = null;
        foreach (ArchitectureHealthValidationOutcome receipt in receipts)
        {
            ProcessReceipt(receipt, ref evaluationDate, ref horizon, reasons);
        }

        if (reasons.Count > 0)
        {
            return Unassessable(reasons);
        }

        if (evaluationDate is null || horizon is null)
        {
            reasons.Add(new(MissingEvaluationDate, "A complete waiver evaluation must carry an explicit evaluation date."));
            return Unassessable(reasons);
        }

        return new ArchitectureHealthPublicationEvidence(
            ArchitectureHealthPublicationEvidenceState.Ready,
            horizon,
            Array.Empty<ArchitectureHealthPublicationEvidenceReason>());
    }

    // One receipt's worth of the foreach body above, extracted so the mode/outcome/inventory/
    // lifecycle/external-evidence branching it carries no longer inflates Project's own cognitive
    // complexity. Behavior is unchanged: a missing outcome still short-circuits the rest of this
    // receipt's checks exactly as the inline `continue` used to.
    private static void ProcessReceipt(
        ArchitectureHealthValidationOutcome receipt,
        ref DateOnly? evaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(receipt.Mode))
        {
            reasons.Add(new(MalformedWaiverReceipt, "A validation receipt must identify its evaluation mode."));
        }

        ValidationOutcome validation = receipt.Outcome;
        if (validation is null)
        {
            reasons.Add(new(MissingValidationReceipt, "A validation receipt has no canonical outcome."));
            return;
        }

        ArchitecturePolicyInventory? inventory = validation.PolicyInventory;
        if (inventory is null)
        {
            reasons.Add(new(MissingPolicyInventory, $"The '{receipt.Mode}' validation receipt has no policy inventory."));
        }
        else
        {
            ValidateInventory(inventory, receipt.Mode, reasons);
        }

        ArchitectureWaiverLifecycleAssessment? lifecycle = validation.WaiverLifecycleAssessment;
        if (lifecycle is null)
        {
            reasons.Add(new(MissingWaiverReceipt, $"The '{receipt.Mode}' validation receipt has no waiver lifecycle receipt."));
        }
        else
        {
            ProcessWaivers(inventory, lifecycle, receipt.Mode, ref evaluationDate, ref horizon, reasons);
        }

        ProcessExternalEvidence(validation, receipt.Mode, reasons);
    }

    private static void ValidateInventory(
        ArchitecturePolicyInventory inventory,
        string mode,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        if (!string.Equals(inventory.SchemaId, ArchitecturePolicyInventory.CurrentSchemaId, StringComparison.Ordinal)
            || inventory.EffectiveRuleCount < 0
            || inventory.Rules is null
            || inventory.IgnoreDebt is null
            || inventory.Waivers is null
            || inventory.Rules.Strict < 0
            || inventory.Rules.Audit < 0
            || inventory.Rules.Coverage < 0
            || inventory.IgnoreDebt.Total < 0)
        {
            reasons.Add(new(InvalidPolicyInventory, $"The '{mode}' policy inventory is not a trusted v1 receipt."));
        }
    }

    private static void ProcessWaivers(
        ArchitecturePolicyInventory? inventory,
        ArchitectureWaiverLifecycleAssessment lifecycle,
        string mode,
        ref DateOnly? evaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(lifecycle.Profile))
        {
            reasons.Add(new(MalformedWaiverReceipt, $"The '{mode}' waiver lifecycle receipt has no profile."));
        }

        ArchitectureWaiverLifecycleRecord[] records = lifecycle.Records is null
            ? Array.Empty<ArchitectureWaiverLifecycleRecord>()
            : lifecycle.Records.Where(record => record is not null).ToArray();

        ProcessLifecycleEvaluationDate(lifecycle, mode, ref evaluationDate, ref horizon, reasons);

        if (records.Length == 0 && lifecycle.EvaluationDate is null)
        {
            reasons.Add(new(MissingEvaluationDate, $"The '{mode}' waiver lifecycle receipt has no explicit evaluation date."));
        }

        if (inventory is not null && inventory.Waivers is not null
            && !WaiverSetsEqual(inventory.Waivers, records))
        {
            reasons.Add(new(InconsistentWaiverReceipt, $"The '{mode}' policy inventory and waiver lifecycle receipts differ."));
        }

        foreach (ArchitectureWaiverLifecycleRecord record in records)
        {
            ProcessWaiverRecord(record, mode, ref evaluationDate, ref horizon, reasons);
        }
    }

    // The `lifecycle.EvaluationDate is {...}` branch of ProcessWaivers above, extracted on its own:
    // it independently establishes the shared evaluation date and folds a candidate horizon in,
    // with the same invalid/inconsistent-date reasons as before.
    private static void ProcessLifecycleEvaluationDate(
        ArchitectureWaiverLifecycleAssessment lifecycle,
        string mode,
        ref DateOnly? evaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        if (lifecycle.EvaluationDate is not { } lifecycleEvaluationDate)
        {
            return;
        }

        if (lifecycleEvaluationDate == DateOnly.MinValue)
        {
            reasons.Add(new(InvalidEvaluationDate, $"The '{mode}' waiver lifecycle receipt has an invalid evaluation date."));
            return;
        }

        if (evaluationDate is null)
        {
            evaluationDate = lifecycleEvaluationDate;
        }
        else if (evaluationDate.Value != lifecycleEvaluationDate)
        {
            reasons.Add(new(InconsistentEvaluationDate, "Waiver lifecycle receipts do not share one evaluation date."));
        }

        if (!TryGetNextUtcDay(lifecycleEvaluationDate, out DateTimeOffset lifecycleHorizon))
        {
            reasons.Add(new(InvalidEvaluationDate, "The waiver evaluation date cannot produce a finite UTC horizon."));
        }
        else
        {
            horizon = horizon is null || lifecycleHorizon < horizon.Value ? lifecycleHorizon : horizon;
        }
    }

    // One waiver record's worth of the foreach body above. An incomplete record or one whose
    // evaluation date cannot produce a finite horizon still stops there, matching the inline
    // `continue`s this replaces.
    private static void ProcessWaiverRecord(
        ArchitectureWaiverLifecycleRecord record,
        string mode,
        ref DateOnly? evaluationDate,
        ref DateTimeOffset? horizon,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(record.Id)
            || string.IsNullOrWhiteSpace(record.State)
            || record.EvaluationDate == DateOnly.MinValue)
        {
            reasons.Add(new(record.EvaluationDate == DateOnly.MinValue ? MissingEvaluationDate : MalformedWaiverReceipt,
                $"The '{mode}' waiver lifecycle receipt contains an incomplete record."));
            return;
        }

        if (evaluationDate is null)
        {
            evaluationDate = record.EvaluationDate;
        }
        else if (evaluationDate.Value != record.EvaluationDate)
        {
            reasons.Add(new(InconsistentEvaluationDate, "Waiver lifecycle receipts do not share one evaluation date."));
        }

        if (!TryGetNextUtcDay(record.EvaluationDate, out DateTimeOffset dateHorizon))
        {
            reasons.Add(new(InvalidEvaluationDate, "The waiver evaluation date cannot produce a finite UTC horizon."));
            return;
        }

        if (record.State is not ("active" or "stale"))
        {
            reasons.Add(new(record.State == "expired" ? ExpiredWaiver : MalformedWaiverReceipt,
                $"The waiver '{record.Id}' is not a currently assessable lifecycle receipt."));
        }

        if (record.Expires is { } expiry)
        {
            if (expiry < record.EvaluationDate || record.State == "expired")
            {
                reasons.Add(new(ExpiredWaiver, $"The waiver '{record.Id}' expired before the supplied evaluation date."));
            }
            else if (!TryGetNextUtcDay(expiry, out DateTimeOffset expiryHorizon))
            {
                reasons.Add(new(InvalidEvaluationDate, $"The waiver '{record.Id}' expiry cannot produce a finite UTC horizon."));
            }
            else
            {
                dateHorizon = expiryHorizon < dateHorizon ? expiryHorizon : dateHorizon;
            }
        }

        horizon = horizon is null || dateHorizon < horizon.Value ? dateHorizon : horizon;
    }

    private static void ProcessExternalEvidence(
        ValidationOutcome validation,
        string mode,
        List<ArchitectureHealthPublicationEvidenceReason> reasons)
    {
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements =
            validation.ExternalEvidenceRequirements ?? Array.Empty<ArchitectureExternalEvidenceRequirement>();
        IReadOnlyList<SarifEvidenceReadResult> trustReceipts =
            validation.ExternalEvidenceTrustReceipts ?? Array.Empty<SarifEvidenceReadResult>();

        ArchitectureExternalEvidenceRequirement[] orderedRequirements = requirements
            .Where(requirement => requirement is not null)
            .OrderBy(requirement => requirement.Id, StringComparer.Ordinal)
            .ToArray();
        if (orderedRequirements.Any(requirement => string.IsNullOrWhiteSpace(requirement.Id))
            || orderedRequirements.Select(requirement => requirement.Id).Distinct(StringComparer.Ordinal).Count()
                != orderedRequirements.Length)
        {
            reasons.Add(new(InconsistentExternalEvidenceReceipt, $"The '{mode}' external-evidence requirements are not uniquely identified."));
        }

        string[] receiptIds = trustReceipts
            .Where(receipt => receipt is not null)
            .Select(receipt => receipt.LogicalId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (receiptIds.Any(string.IsNullOrWhiteSpace)
            || receiptIds.Distinct(StringComparer.Ordinal).Count() != receiptIds.Length
            || (receiptIds.Length > 0 && orderedRequirements.Length == 0))
        {
            reasons.Add(new(InconsistentExternalEvidenceReceipt, $"The '{mode}' external-evidence trust receipts do not match requirements."));
        }

        orderedRequirements.Where(item => item.Required).ToList().ForEach(requirement =>
        {
            if (!receiptIds.Contains(requirement.Id, StringComparer.Ordinal))
            {
                reasons.Add(new(MissingExternalEvidenceReceipt, $"Required external evidence '{requirement.Id}' has no trust receipt."));
            }

            // SARIF trust receipts deliberately carry no expiry. A required artifact therefore
            // makes a bounded semantic horizon impossible until a future receipt supplies one.
            reasons.Add(new(RequiredExternalEvidenceHorizonUnknown,
                $"Required external evidence '{requirement.Id}' has no finite reuse horizon."));
        });
    }

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

    private static string FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool TryGetNextUtcDay(DateOnly date, out DateTimeOffset value)
    {
        if (date >= DateOnly.MaxValue)
        {
            value = default;
            return false;
        }

        DateOnly next = date.AddDays(1);
        value = new DateTimeOffset(next.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return true;
    }

    private static ArchitectureHealthPublicationEvidence Unassessable(
        IEnumerable<ArchitectureHealthPublicationEvidenceReason> reasons) =>
        new(
            ArchitectureHealthPublicationEvidenceState.Unassessable,
            null,
            reasons
                .GroupBy(reason => (reason.Code, reason.Detail))
                .Select(group => group.First())
                .ToArray());
}
