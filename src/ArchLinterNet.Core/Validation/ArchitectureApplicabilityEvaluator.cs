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

        List<ArchitectureApplicabilityAssessment> assessments = BuildExpectedAssessments(
            expectedByIdentity,
            recordsByIdentity);
        AddOrphanAssessments(assessments, expectedByIdentity, recordsByIdentity);
        assessments = OrderAssessments(assessments);

        IReadOnlyList<ArchitectureApplicabilityReason> reasons = CollectReasons(assessments);
        ArchitectureAssessmentCompletionState state = DeriveState(assessments, conformancePassed);

        return new ArchitectureAssessmentCompletionEvidence(state, assessments, reasons);
    }

    /// <summary>Alias emphasizing that this operation derives completion rather than findings.</summary>
    public static ArchitectureAssessmentCompletionEvidence? DeriveCompletion(
        IReadOnlyCollection<ArchitectureApplicabilityExpectedEntry> expectedEntries,
        IReadOnlyCollection<ArchitectureApplicabilityRecord> records,
        bool conformancePassed) => Evaluate(expectedEntries, records, conformancePassed);

    private static List<ArchitectureApplicabilityAssessment> BuildExpectedAssessments(
        IReadOnlyDictionary<string, List<ArchitectureApplicabilityExpectedEntry>> expectedByIdentity,
        IReadOnlyDictionary<string, List<ArchitectureApplicabilityRecord>> recordsByIdentity)
    {
        List<ArchitectureApplicabilityAssessment> assessments = new();
        foreach (string identity in expectedByIdentity.Keys.Order(StringComparer.Ordinal))
        {
            recordsByIdentity.TryGetValue(identity, out List<ArchitectureApplicabilityRecord>? recordMatches);
            assessments.Add(BuildExpectedAssessment(expectedByIdentity[identity], recordMatches));
        }

        return assessments;
    }

    private static ArchitectureApplicabilityAssessment BuildExpectedAssessment(
        IReadOnlyCollection<ArchitectureApplicabilityExpectedEntry> expectedEntries,
        IReadOnlyCollection<ArchitectureApplicabilityRecord>? recordMatches)
    {
        ArchitectureApplicabilityExpectedEntry[] orderedExpected = OrderExpectedEntries(expectedEntries).ToArray();
        ArchitectureApplicabilityExpectedEntry expected = orderedExpected[0];
        List<ArchitectureApplicabilityReason> integrityReasons = new();
        AddExpectedIdentityDefects(orderedExpected, integrityReasons);

        ArchitectureApplicabilityRecord? record = ResolveRecord(expected, recordMatches, integrityReasons);
        AddUnassessableRecordDefects(record, integrityReasons);
        return new ArchitectureApplicabilityAssessment(expected, record, integrityReasons);
    }

    private static void AddExpectedIdentityDefects(
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expectedEntries,
        List<ArchitectureApplicabilityReason> integrityReasons)
    {
        foreach (ArchitectureApplicabilityExpectedEntry expected in expectedEntries)
        {
            if (expectedEntries.Count > 1)
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityExpectedIdentity,
                    expected.Provenance));
            }

            if (!HasCanonicalProvenance(expected))
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.InvalidApplicabilityExpectedIntegrity,
                    new ArchitectureApplicabilityProvenance(expected.Family, expected.ControlIdentity)));
            }
        }
    }

    private static bool HasCanonicalProvenance(ArchitectureApplicabilityExpectedEntry expected)
    {
        return string.Equals(expected.Provenance.Family, expected.Family, StringComparison.Ordinal)
            && string.Equals(expected.Provenance.ControlIdentity, expected.ControlIdentity, StringComparison.Ordinal);
    }

    private static ArchitectureApplicabilityRecord? ResolveRecord(
        ArchitectureApplicabilityExpectedEntry expected,
        IReadOnlyCollection<ArchitectureApplicabilityRecord>? recordMatches,
        List<ArchitectureApplicabilityReason> integrityReasons)
    {
        if (recordMatches is null || recordMatches.Count == 0)
        {
            integrityReasons.Add(CreateReason(
                ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord,
                expected.Provenance));
            return null;
        }

        if (recordMatches.Count > 1)
        {
            foreach (ArchitectureApplicabilityRecord duplicate in OrderRecords(recordMatches))
            {
                integrityReasons.Add(CreateReason(
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity,
                    duplicate.Provenance));
            }

            return null;
        }

        ArchitectureApplicabilityRecord record = recordMatches.Single();
        AddCompatibilityDefects(expected, record, integrityReasons);
        return record;
    }

    private static void AddUnassessableRecordDefects(
        ArchitectureApplicabilityRecord? record,
        List<ArchitectureApplicabilityReason> integrityReasons)
    {
        if (record?.State != ArchitectureApplicabilityRecordState.Unassessable)
        {
            return;
        }

        if (record.Reasons.Count == 0 || record.Reasons.Any(reason =>
                !HasCanonicalReasonProvenance(reason, record.Provenance)))
        {
            integrityReasons.Add(CreateReason(
                ArchitectureApplicabilityReasonCodes.InvalidApplicabilityRecordIntegrity,
                record.Provenance));
        }
    }

    private static bool HasCanonicalReasonProvenance(
        ArchitectureApplicabilityReason reason,
        ArchitectureApplicabilityProvenance recordProvenance)
    {
        return string.Equals(reason.Provenance.Family, recordProvenance.Family, StringComparison.Ordinal)
            && string.Equals(
                reason.Provenance.ControlIdentity,
                recordProvenance.ControlIdentity,
                StringComparison.Ordinal)
            && string.Equals(
                reason.Provenance.PolicyIdentity,
                recordProvenance.PolicyIdentity,
                StringComparison.Ordinal);
    }

    private static void AddOrphanAssessments(
        List<ArchitectureApplicabilityAssessment> assessments,
        IReadOnlyDictionary<string, List<ArchitectureApplicabilityExpectedEntry>> expectedByIdentity,
        IReadOnlyDictionary<string, List<ArchitectureApplicabilityRecord>> recordsByIdentity)
    {
        foreach (string identity in recordsByIdentity.Keys.Order(StringComparer.Ordinal))
        {
            if (expectedByIdentity.ContainsKey(identity))
            {
                continue;
            }

            foreach (ArchitectureApplicabilityRecord record in OrderRecords(recordsByIdentity[identity]))
            {
                ArchitectureApplicabilityReason reason = CreateReason(
                    ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity,
                    record.Provenance);
                assessments.Add(new ArchitectureApplicabilityAssessment(null, record, [reason]));
            }
        }
    }

    private static List<ArchitectureApplicabilityAssessment> OrderAssessments(
        IEnumerable<ArchitectureApplicabilityAssessment> assessments)
    {
        return assessments
            .OrderBy(assessment => assessment.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.Expected?.Family, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.Record?.Family, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ArchitectureApplicabilityReason> CollectReasons(
        IEnumerable<ArchitectureApplicabilityAssessment> assessments)
    {
        return assessments
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
    }

    private static ArchitectureAssessmentCompletionState DeriveState(
        IEnumerable<ArchitectureApplicabilityAssessment> assessments,
        bool conformancePassed)
    {
        bool hasInsufficientEvidence = assessments.Any(assessment =>
            !assessment.IsIntegrityValid
            || assessment.State == ArchitectureApplicabilityRecordState.Unassessable);
        if (hasInsufficientEvidence)
        {
            return ArchitectureAssessmentCompletionState.Unassessable;
        }

        return conformancePassed
            ? ArchitectureAssessmentCompletionState.Pass
            : ArchitectureAssessmentCompletionState.Fail;
    }

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

    private static IOrderedEnumerable<ArchitectureApplicabilityExpectedEntry> OrderExpectedEntries(
        IEnumerable<ArchitectureApplicabilityExpectedEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.Membership)
            .ThenBy(entry => entry.Family, StringComparer.Ordinal)
            .ThenBy(entry => entry.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(entry => entry.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(entry => entry.Provenance.PolicyIdentity, StringComparer.Ordinal);
    }

    private static IOrderedEnumerable<ArchitectureApplicabilityRecord> OrderRecords(
        IEnumerable<ArchitectureApplicabilityRecord> records)
    {
        return records
            .OrderBy(record => record.State)
            .ThenBy(record => record.Family, StringComparer.Ordinal)
            .ThenBy(record => record.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(record => record.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(record => record.Provenance.PolicyIdentity, StringComparer.Ordinal);
    }

    private static void AddCompatibilityDefects(
        ArchitectureApplicabilityExpectedEntry expected,
        ArchitectureApplicabilityRecord record,
        List<ArchitectureApplicabilityReason> defects)
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
