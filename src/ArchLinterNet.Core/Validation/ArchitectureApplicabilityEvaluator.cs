using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Derives trusted assessment completion from canonical applicability inputs.
/// </summary>
/// <remarks>
/// This evaluator deliberately knows nothing about YAML or any one governance family. Families
/// publish expected membership and native applicability records; this boundary only validates the
/// identity join and the membership/state matrix. A null result means that no family opted into
/// applicability semantics, which preserves the pre-v0.8 validation contract.
/// </remarks>
public static class ArchitectureApplicabilityEvaluator
{
    /// <summary>
    /// Validates expected membership and produced records, then derives completion using the
    /// ordinary conformance result supplied by the caller.
    /// </summary>
    /// <param name="expectedEntries">Canonical expected effective-control membership.</param>
    /// <param name="records">Family-produced applicability records.</param>
    /// <param name="conformancePassed">The ordinary architecture conformance result.</param>
    /// <returns>
    /// Null when both inputs are empty (no applicability opt-in); otherwise immutable completion
    /// evidence. Integrity defects are represented as evidence and never thrown as violations.
    /// </returns>
    public static ArchitectureAssessmentCompletionEvidence? Evaluate(
        IReadOnlyCollection<ArchitectureApplicabilityExpectedEntry> expectedEntries,
        IReadOnlyCollection<ArchitectureApplicabilityRecord> records,
        bool conformancePassed)
    {
        ArgumentNullException.ThrowIfNull(expectedEntries);
        ArgumentNullException.ThrowIfNull(records);

        if (expectedEntries.Count == 0 && records.Count == 0)
        {
            return null;
        }

        Dictionary<string, List<ArchitectureApplicabilityExpectedEntry>> expectedByIdentity =
            GroupByIdentity(expectedEntries);
        Dictionary<string, List<ArchitectureApplicabilityRecord>> recordsByIdentity =
            GroupByIdentity(records);

        List<ArchitectureApplicabilityAssessment> assessments = new();
        foreach (string identity in expectedByIdentity.Keys.Order(StringComparer.Ordinal))
        {
            List<ArchitectureApplicabilityExpectedEntry> expectedMatches = expectedByIdentity[identity];
            ArchitectureApplicabilityExpectedEntry expected = expectedMatches[0];
            List<ArchitectureApplicabilityReason> integrityReasons = new();

            if (expectedMatches.Count > 1)
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityExpectedIdentity,
                    expected.Provenance));
            }

            recordsByIdentity.TryGetValue(identity, out List<ArchitectureApplicabilityRecord>? recordMatches);
            ArchitectureApplicabilityRecord? record = null;
            if (recordMatches is null or { Count: 0 })
            {
                if (expected.Membership == ArchitectureApplicabilityMembership.Required)
                {
                    integrityReasons.Add(CreateReason(
                        ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord,
                        expected.Provenance));
                }
            }
            else if (recordMatches.Count > 1)
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity,
                    expected.Provenance));
            }
            else
            {
                record = recordMatches[0];
                AddCompatibilityDefects(expected, record, integrityReasons);
            }

            // An unassessable record without a reason would make the completion impossible to
            // explain. Preserve it as integrity evidence rather than accepting an opaque state.
            if (record?.State == ArchitectureApplicabilityRecordState.Unassessable
                && record.Reasons.Count == 0)
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.InvalidApplicabilityRecordIntegrity,
                    record.Provenance));
            }

            assessments.Add(new ArchitectureApplicabilityAssessment(expected, record, integrityReasons));
        }

        // The expected-to-produced left join cannot see records with no expected identity. Keep
        // every orphan as a deterministic assessment row so a consumer cannot accidentally drop
        // malformed producer output before checking collection integrity.
        foreach (string identity in recordsByIdentity.Keys.Order(StringComparer.Ordinal))
        {
            if (expectedByIdentity.ContainsKey(identity))
            {
                continue;
            }

            foreach (ArchitectureApplicabilityRecord record in recordsByIdentity[identity]
                         .OrderBy(item => item.Family, StringComparer.Ordinal)
                         .ThenBy(item => item.Provenance.PolicyIdentity, StringComparer.Ordinal))
            {
                ArchitectureApplicabilityReason reason = CreateReason(
                    ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity,
                    record.Provenance);
                assessments.Add(new ArchitectureApplicabilityAssessment(null, record, [reason]));
            }
        }

        assessments = assessments
            .OrderBy(assessment => assessment.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.Expected?.Family, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.Record?.Family, StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<ArchitectureApplicabilityReason> reasons = assessments
            .SelectMany(assessment => assessment.IntegrityReasons)
            .Concat(assessments
                .Where(assessment => assessment.IsIntegrityValid
                    && assessment.Record?.State == ArchitectureApplicabilityRecordState.Unassessable)
                .SelectMany(assessment => assessment.Record!.Reasons))
            .OrderBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Code, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ToArray();

        bool hasInsufficientEvidence = assessments.Any(assessment =>
            !assessment.IsIntegrityValid
            || assessment.State == ArchitectureApplicabilityRecordState.Unassessable);
        ArchitectureAssessmentCompletionState state = hasInsufficientEvidence
            ? ArchitectureAssessmentCompletionState.Unassessable
            : conformancePassed
                ? ArchitectureAssessmentCompletionState.Pass
                : ArchitectureAssessmentCompletionState.Fail;

        return new ArchitectureAssessmentCompletionEvidence(state, assessments, reasons);
    }

    /// <summary>Alias emphasizing that this operation derives completion rather than findings.</summary>
    public static ArchitectureAssessmentCompletionEvidence? DeriveCompletion(
        IReadOnlyCollection<ArchitectureApplicabilityExpectedEntry> expectedEntries,
        IReadOnlyCollection<ArchitectureApplicabilityRecord> records,
        bool conformancePassed) => Evaluate(expectedEntries, records, conformancePassed);

    private static Dictionary<string, List<T>> GroupByIdentity<T>(IEnumerable<T> values)
        where T : notnull
    {
        Dictionary<string, List<T>> grouped = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string identity = value switch
            {
                ArchitectureApplicabilityExpectedEntry expected => expected.ControlIdentity,
                ArchitectureApplicabilityRecord record => record.ControlIdentity,
                _ => throw new ArgumentException($"Unsupported applicability value: {typeof(T).Name}.", nameof(values)),
            };

            if (!grouped.TryGetValue(identity, out List<T>? matches))
            {
                matches = new List<T>();
                grouped.Add(identity, matches);
            }

            matches.Add(value);
        }

        return grouped;
    }

    private static void AddCompatibilityDefects(
        ArchitectureApplicabilityExpectedEntry expected,
        ArchitectureApplicabilityRecord record,
        ICollection<ArchitectureApplicabilityReason> defects)
    {
        if (!string.Equals(expected.Family, record.Family, StringComparison.Ordinal)
            || !string.Equals(record.Provenance.Family, record.Family, StringComparison.Ordinal)
            || !string.Equals(record.Provenance.ControlIdentity, record.ControlIdentity, StringComparison.Ordinal))
        {
            defects.Add(CreateReason(
                ArchitectureApplicabilityReasonCodes.IncompatibleApplicabilityRecord,
                record.Provenance));
            return;
        }

        bool stateCompatible = expected.Membership switch
        {
            ArchitectureApplicabilityMembership.Required =>
                record.State is ArchitectureApplicabilityRecordState.Evaluable
                    or ArchitectureApplicabilityRecordState.Unassessable,
            ArchitectureApplicabilityMembership.Optional =>
                record.State is ArchitectureApplicabilityRecordState.Evaluable
                    or ArchitectureApplicabilityRecordState.NotApplicable
                    or ArchitectureApplicabilityRecordState.Unassessable,
            ArchitectureApplicabilityMembership.NotApplicable =>
                record.State == ArchitectureApplicabilityRecordState.NotApplicable,
            _ => false,
        };

        if (!stateCompatible)
        {
            defects.Add(CreateReason(
                ArchitectureApplicabilityReasonCodes.IncompatibleApplicabilityRecord,
                record.Provenance));
        }
    }

    private static ArchitectureApplicabilityReason CreateReason(
        string code,
        ArchitectureApplicabilityProvenance provenance) =>
        new(code, provenance);
}
