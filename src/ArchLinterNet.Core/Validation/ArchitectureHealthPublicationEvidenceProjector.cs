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
        void Add(string code, string detail) => reasons.Add(new(code, detail));

        ArchitectureHealthValidationOutcome[] receipts = outcome.ValidationOutcomes is null
            ? Array.Empty<ArchitectureHealthValidationOutcome>()
            : outcome.ValidationOutcomes
                .Where(receipt => receipt is not null)
                .OrderBy(receipt => receipt.Mode, StringComparer.Ordinal)
                .ToArray();

        if (receipts.Length == 0)
        {
            Add(MissingValidationReceipt, "At least one complete validation receipt is required.");
            return Unassessable(reasons);
        }

        DateOnly? evaluationDate = null;
        DateTimeOffset? horizon = null;
        foreach (ArchitectureHealthValidationOutcome receipt in receipts)
        {
            if (string.IsNullOrWhiteSpace(receipt.Mode))
            {
                Add(MalformedWaiverReceipt, "A validation receipt must identify its evaluation mode.");
            }

            ValidationOutcome validation = receipt.Outcome;
            if (validation is null)
            {
                Add(MissingValidationReceipt, "A validation receipt has no canonical outcome.");
                continue;
            }

            ArchitecturePolicyInventory? inventory = validation.PolicyInventory;
            if (inventory is null)
            {
                Add(MissingPolicyInventory, $"The '{receipt.Mode}' validation receipt has no policy inventory.");
            }
            else
            {
                ValidateInventory(inventory, receipt.Mode, Add);
            }

            ArchitectureWaiverLifecycleAssessment? lifecycle = validation.WaiverLifecycleAssessment;
            if (lifecycle is null)
            {
                Add(MissingWaiverReceipt, $"The '{receipt.Mode}' validation receipt has no waiver lifecycle receipt.");
            }
            else
            {
                ProcessWaivers(inventory, lifecycle, receipt.Mode, ref evaluationDate, ref horizon, Add);
            }

            ProcessExternalEvidence(validation, receipt.Mode, Add);
        }

        if (reasons.Count > 0)
        {
            return Unassessable(reasons);
        }

        if (evaluationDate is null || horizon is null)
        {
            Add(MissingEvaluationDate, "A complete waiver evaluation must carry an explicit evaluation date.");
            return Unassessable(reasons);
        }

        return new ArchitectureHealthPublicationEvidence(
            ArchitectureHealthPublicationEvidenceState.Ready,
            horizon,
            Array.Empty<ArchitectureHealthPublicationEvidenceReason>());
    }

    private static void ValidateInventory(
        ArchitecturePolicyInventory inventory,
        string mode,
        Action<string, string> add)
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
            add(InvalidPolicyInventory, $"The '{mode}' policy inventory is not a trusted v1 receipt.");
        }
    }

    private static void ProcessWaivers(
        ArchitecturePolicyInventory? inventory,
        ArchitectureWaiverLifecycleAssessment lifecycle,
        string mode,
        ref DateOnly? evaluationDate,
        ref DateTimeOffset? horizon,
        Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(lifecycle.Profile))
        {
            add(MalformedWaiverReceipt, $"The '{mode}' waiver lifecycle receipt has no profile.");
        }

        ArchitectureWaiverLifecycleRecord[] records = lifecycle.Records is null
            ? Array.Empty<ArchitectureWaiverLifecycleRecord>()
            : lifecycle.Records.Where(record => record is not null).ToArray();

        if (records.Length == 0)
        {
            add(MissingEvaluationDate, $"The '{mode}' waiver lifecycle receipt has no explicit evaluation date.");
        }

        if (inventory is not null && inventory.Waivers is not null
            && !WaiverSetsEqual(inventory.Waivers, records))
        {
            add(InconsistentWaiverReceipt, $"The '{mode}' policy inventory and waiver lifecycle receipts differ.");
        }

        foreach (ArchitectureWaiverLifecycleRecord record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id)
                || string.IsNullOrWhiteSpace(record.State)
                || record.EvaluationDate == DateOnly.MinValue)
            {
                add(record.EvaluationDate == DateOnly.MinValue ? MissingEvaluationDate : MalformedWaiverReceipt,
                    $"The '{mode}' waiver lifecycle receipt contains an incomplete record.");
                continue;
            }

            if (evaluationDate is null)
            {
                evaluationDate = record.EvaluationDate;
            }
            else if (evaluationDate.Value != record.EvaluationDate)
            {
                add(InconsistentEvaluationDate, "Waiver lifecycle receipts do not share one evaluation date.");
            }

            if (!TryGetNextUtcDay(record.EvaluationDate, out DateTimeOffset dateHorizon))
            {
                add(InvalidEvaluationDate, "The waiver evaluation date cannot produce a finite UTC horizon.");
                continue;
            }

            if (record.State is not ("active" or "stale"))
            {
                add(record.State == "expired" ? ExpiredWaiver : MalformedWaiverReceipt,
                    $"The waiver '{record.Id}' is not a currently assessable lifecycle receipt.");
            }

            if (record.Expires is { } expiry)
            {
                if (expiry < record.EvaluationDate || record.State == "expired")
                {
                    add(ExpiredWaiver, $"The waiver '{record.Id}' expired before the supplied evaluation date.");
                }
                else if (!TryGetNextUtcDay(expiry, out DateTimeOffset expiryHorizon))
                {
                    add(InvalidEvaluationDate, $"The waiver '{record.Id}' expiry cannot produce a finite UTC horizon.");
                }
                else
                {
                    dateHorizon = expiryHorizon < dateHorizon ? expiryHorizon : dateHorizon;
                }
            }

            horizon = horizon is null || dateHorizon < horizon.Value ? dateHorizon : horizon;
        }
    }

    private static void ProcessExternalEvidence(
        ValidationOutcome validation,
        string mode,
        Action<string, string> add)
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
            add(InconsistentExternalEvidenceReceipt, $"The '{mode}' external-evidence requirements are not uniquely identified.");
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
            add(InconsistentExternalEvidenceReceipt, $"The '{mode}' external-evidence trust receipts do not match requirements.");
        }

        foreach (ArchitectureExternalEvidenceRequirement requirement in orderedRequirements.Where(item => item.Required))
        {
            if (!receiptIds.Contains(requirement.Id, StringComparer.Ordinal))
            {
                add(MissingExternalEvidenceReceipt, $"Required external evidence '{requirement.Id}' has no trust receipt.");
            }

            // SARIF trust receipts deliberately carry no expiry. A required artifact therefore
            // makes a bounded semantic horizon impossible until a future receipt supplies one.
            add(RequiredExternalEvidenceHorizonUnknown,
                $"Required external evidence '{requirement.Id}' has no finite reuse horizon.");
        }
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
