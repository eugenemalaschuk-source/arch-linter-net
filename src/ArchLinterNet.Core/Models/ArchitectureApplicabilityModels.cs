namespace ArchLinterNet.Core.Model;

/// <summary>Membership of an effective control in an applicability assessment.</summary>
public enum ArchitectureApplicabilityMembership
{
    Required,
    Optional,
    NotApplicable,
}

/// <summary>State produced by a family for one applicability record.</summary>
public enum ArchitectureApplicabilityRecordState
{
    Evaluable,
    NotApplicable,
    Unassessable,
}

/// <summary>Completion state of a valid authoritative assessment.</summary>
public enum ArchitectureAssessmentCompletionState
{
    Pass,
    Fail,
    Unassessable,
}

/// <summary>Stable machine-readable reason codes used by applicability evidence.</summary>
public static class ArchitectureApplicabilityReasonCodes
{
    public const string MissingRequiredInput = "missing_required_input";
    public const string UnexpectedEmptyInput = "unexpected_empty_input";
    public const string UnmappedSubject = "unmapped_subject";
    public const string AmbiguousSubject = "ambiguous_subject";
    public const string StaleDeclaration = "stale_declaration";
    public const string MalformedExternalInput = "malformed_external_input";
    public const string WrongExternalRepository = "wrong_external_repository";
    public const string WrongExternalRevision = "wrong_external_revision";
    public const string WrongExternalScope = "wrong_external_scope";
    public const string MissingApplicabilityRecord = "missing_applicability_record";
    public const string DuplicateApplicabilityRecordIdentity = "duplicate_applicability_record_identity";
    public const string UnknownApplicabilityRecordIdentity = "unknown_applicability_record_identity";
    public const string IncompatibleApplicabilityRecord = "incompatible_applicability_record";
    public const string InvalidApplicabilityRecordIntegrity = "invalid_applicability_record_integrity";
    public const string DuplicateApplicabilityExpectedIdentity = "duplicate_applicability_expected_identity";
}

/// <summary>
/// Canonical provenance for applicability evidence. The control identity is the effective-policy
/// identity and is deliberately independent of display text, finding count, or enumeration order.
/// </summary>
public sealed record ArchitectureApplicabilityProvenance
{
    public ArchitectureApplicabilityProvenance(
        string family,
        string controlIdentity,
        string policyIdentity = "")
    {
        Family = RequireValue(family, nameof(family));
        ControlIdentity = RequireValue(controlIdentity, nameof(controlIdentity));
        PolicyIdentity = policyIdentity ?? string.Empty;
    }

    public string Family { get; }

    public string ControlIdentity { get; }

    public string PolicyIdentity { get; }

    // These aliases make the canonical identity vocabulary explicit to consumers that call it an
    // effective-control id. They do not introduce a second identity.
    public string EffectiveControlId => ControlIdentity;

    public string PolicyId => PolicyIdentity;

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A canonical applicability value is required.", parameterName)
            : value;
}

/// <summary>A deterministic machine-readable reason and the provenance that produced it.</summary>
public sealed record ArchitectureApplicabilityReason
{
    public ArchitectureApplicabilityReason(string code, ArchitectureApplicabilityProvenance provenance)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An applicability reason code is required.", nameof(code));
        }

        Code = code;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public ArchitectureApplicabilityReason(
        string code,
        string family,
        string controlIdentity,
        string policyIdentity = "")
        : this(code, new ArchitectureApplicabilityProvenance(family, controlIdentity, policyIdentity))
    {
    }

    public string Code { get; }

    public ArchitectureApplicabilityProvenance Provenance { get; }
}

/// <summary>
/// Canonical expected membership for one effective control. Membership is supplied by the
/// family/effective-policy authority and never inferred from produced records.
/// </summary>
public sealed record ArchitectureApplicabilityExpectedEntry
{
    public ArchitectureApplicabilityExpectedEntry(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityMembership membership,
        ArchitectureApplicabilityProvenance provenance)
    {
        ControlIdentity = RequireValue(controlIdentity, nameof(controlIdentity));
        Family = RequireValue(family, nameof(family));
        Membership = membership;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public ArchitectureApplicabilityExpectedEntry(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityMembership membership)
        : this(controlIdentity, family, membership, new ArchitectureApplicabilityProvenance(family, controlIdentity))
    {
    }

    public string ControlIdentity { get; }

    public string EffectiveControlId => ControlIdentity;

    public string Family { get; }

    public ArchitectureApplicabilityMembership Membership { get; }

    public ArchitectureApplicabilityProvenance Provenance { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A canonical applicability value is required.", parameterName)
            : value;
}

/// <summary>One family-produced applicability record keyed by an expected control identity.</summary>
public sealed record ArchitectureApplicabilityRecord
{
    public ArchitectureApplicabilityRecord(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityRecordState state,
        IReadOnlyList<ArchitectureApplicabilityReason> reasons,
        ArchitectureApplicabilityProvenance provenance)
    {
        ControlIdentity = RequireValue(controlIdentity, nameof(controlIdentity));
        Family = RequireValue(family, nameof(family));
        State = state;
        Reasons = CopyReasons(reasons);
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public ArchitectureApplicabilityRecord(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityRecordState state,
        ArchitectureApplicabilityProvenance provenance)
        : this(controlIdentity, family, state, Array.Empty<ArchitectureApplicabilityReason>(), provenance)
    {
    }

    public ArchitectureApplicabilityRecord(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityRecordState state,
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
        : this(controlIdentity, family, state, reasons, new ArchitectureApplicabilityProvenance(family, controlIdentity))
    {
    }

    public ArchitectureApplicabilityRecord(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityRecordState state)
        : this(controlIdentity, family, state, Array.Empty<ArchitectureApplicabilityReason>())
    {
    }

    public string ControlIdentity { get; }

    public string EffectiveControlId => ControlIdentity;

    public string Family { get; }

    public ArchitectureApplicabilityRecordState State { get; }

    public IReadOnlyList<ArchitectureApplicabilityReason> Reasons { get; }

    public ArchitectureApplicabilityProvenance Provenance { get; }

    private static IReadOnlyList<ArchitectureApplicabilityReason> CopyReasons(
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        return reasons
            .Where(reason => reason is not null)
            .OrderBy(reason => reason.Code, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A canonical applicability value is required.", parameterName)
            : value;
}

/// <summary>
/// A left-join row between one expected entry and its produced record. Integrity reasons are kept
/// separate from a valid produced state, so missing or incompatible records cannot be reinterpreted
/// as an ordinary state.
/// </summary>
public sealed record ArchitectureApplicabilityAssessment
{
    public ArchitectureApplicabilityAssessment(
        ArchitectureApplicabilityExpectedEntry? expected,
        ArchitectureApplicabilityRecord? record,
        IReadOnlyList<ArchitectureApplicabilityReason> integrityReasons)
    {
        Expected = expected;
        Record = record;
        IntegrityReasons = CopyReasons(integrityReasons);
    }

    public ArchitectureApplicabilityExpectedEntry? Expected { get; }

    public ArchitectureApplicabilityRecord? Record { get; }

    public IReadOnlyList<ArchitectureApplicabilityReason> IntegrityReasons { get; }

    public string ControlIdentity => Expected?.ControlIdentity ?? Record?.ControlIdentity ?? string.Empty;

    public string EffectiveControlId => ControlIdentity;

    public ArchitectureApplicabilityMembership? Membership => Expected?.Membership;

    // A state is only valid when the collection join has no integrity defect.
    public ArchitectureApplicabilityRecordState? State =>
        IntegrityReasons.Count == 0 ? Record?.State : null;

    public bool IsIntegrityValid => IntegrityReasons.Count == 0;

    private static IReadOnlyList<ArchitectureApplicabilityReason> CopyReasons(
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        return reasons
            .Where(reason => reason is not null)
            .OrderBy(reason => reason.Code, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>Derived completion and stable per-control evidence for an authoritative assessment.</summary>
public sealed record ArchitectureAssessmentCompletionEvidence
{
    public ArchitectureAssessmentCompletionEvidence(
        ArchitectureAssessmentCompletionState state,
        IReadOnlyList<ArchitectureApplicabilityAssessment> controls,
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        State = state;
        Controls = CopyControls(controls);
        Reasons = CopyReasons(reasons);
    }

    public ArchitectureAssessmentCompletionState State { get; }

    public ArchitectureAssessmentCompletionState Completion => State;

    public IReadOnlyList<ArchitectureApplicabilityAssessment> Controls { get; }

    public IReadOnlyList<ArchitectureApplicabilityReason> Reasons { get; }

    public int RequiredCount => Controls.Count(control =>
        control.Membership == ArchitectureApplicabilityMembership.Required);

    public int RequiredEvaluableCount => Controls.Count(control =>
        control.Membership == ArchitectureApplicabilityMembership.Required
        && control.State == ArchitectureApplicabilityRecordState.Evaluable);

    public int RequiredUnassessableCount => Controls.Count(control =>
        control.Membership == ArchitectureApplicabilityMembership.Required
        && (control.State == ArchitectureApplicabilityRecordState.Unassessable
            || !control.IsIntegrityValid));

    public bool IsUnassessable => State == ArchitectureAssessmentCompletionState.Unassessable;

    private static IReadOnlyList<ArchitectureApplicabilityAssessment> CopyControls(
        IReadOnlyList<ArchitectureApplicabilityAssessment> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        return controls
            .Where(control => control is not null)
            .OrderBy(control => control.ControlIdentity, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ArchitectureApplicabilityReason> CopyReasons(
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        return reasons
            .Where(reason => reason is not null)
            .OrderBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Code, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ToArray();
    }
}
