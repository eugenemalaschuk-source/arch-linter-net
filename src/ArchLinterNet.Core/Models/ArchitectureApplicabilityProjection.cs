namespace ArchLinterNet.Core.Model;

/// <summary>Stable lower-snake-case wire names for applicability enum values.</summary>
public static class ArchitectureApplicabilityWireNames
{
    public static string MembershipToken(ArchitectureApplicabilityMembership membership) => membership switch
    {
        ArchitectureApplicabilityMembership.Required => "required",
        ArchitectureApplicabilityMembership.Optional => "optional",
        ArchitectureApplicabilityMembership.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(membership), membership, "Unknown applicability membership."),
    };

    public static string StateToken(ArchitectureApplicabilityRecordState state) => state switch
    {
        ArchitectureApplicabilityRecordState.Evaluable => "evaluable",
        ArchitectureApplicabilityRecordState.NotApplicable => "not_applicable",
        ArchitectureApplicabilityRecordState.Unassessable => "unassessable",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown applicability record state."),
    };
}

/// <summary>
/// Deterministic counts over the evaluator's joined control assessments.
/// </summary>
/// <remarks>
/// These are completeness/evaluability counts, not an architecture-quality score.  Membership
/// counts come from expected entries while state counts are taken only from an integrity-valid
/// assessment.  An integrity defect is therefore counted as unassessable and can never disappear
/// by omitting its produced record.
/// </remarks>
public sealed record ArchitectureApplicabilitySummary
{
    public ArchitectureApplicabilitySummary(
        int requiredCount,
        int requiredEvaluableCount,
        int requiredUnassessableCount,
        int evaluableCount,
        int unassessableCount,
        int optionalCount,
        int notApplicableCount)
    {
        RequiredCount = RequireNonNegative(requiredCount, nameof(requiredCount));
        RequiredEvaluableCount = RequireNonNegative(requiredEvaluableCount, nameof(requiredEvaluableCount));
        RequiredUnassessableCount = RequireNonNegative(requiredUnassessableCount, nameof(requiredUnassessableCount));
        EvaluableCount = RequireNonNegative(evaluableCount, nameof(evaluableCount));
        UnassessableCount = RequireNonNegative(unassessableCount, nameof(unassessableCount));
        OptionalCount = RequireNonNegative(optionalCount, nameof(optionalCount));
        NotApplicableCount = RequireNonNegative(notApplicableCount, nameof(notApplicableCount));
    }

    public int RequiredCount { get; }

    public int RequiredEvaluableCount { get; }

    public int RequiredUnassessableCount { get; }

    public int EvaluableCount { get; }

    public int UnassessableCount { get; }

    public int OptionalCount { get; }

    public int NotApplicableCount { get; }

    // Explicit aliases make the denominator/numerator vocabulary clear to report consumers.
    public int RequiredControlCount => RequiredCount;

    public int EvaluableControlCount => EvaluableCount;

    public int UnassessableControlCount => UnassessableCount;

    public int OptionalControlCount => OptionalCount;

    public int NotApplicableControlCount => NotApplicableCount;

    private static int RequireNonNegative(int value, string parameterName) =>
        value < 0
            ? throw new ArgumentOutOfRangeException(parameterName, value, "Applicability counts cannot be negative.")
            : value;
}

/// <summary>
/// A typed normalized diagnostic for one applicability insufficiency reason.
/// </summary>
/// <remarks>
/// Evaluable and deliberately not-applicable controls are represented by the control projection,
/// not by findings.  The diagnostic is reserved for a valid unassessable reason or a collection
/// integrity reason and carries both the raw produced state and the evaluator's validated state.
/// </remarks>
public sealed record ArchitectureApplicabilityDiagnostic : ArchitectureDiagnostic
{
    public ArchitectureApplicabilityDiagnostic(
        string contractName,
        string? contractId,
        string controlIdentity,
        string family,
        ArchitectureApplicabilityMembership? membership,
        ArchitectureApplicabilityRecordState? state,
        ArchitectureApplicabilityRecordState? validatedState,
        ArchitectureApplicabilityReason reason,
        ArchitectureApplicabilityProvenance provenance)
        : base(
            RequireValue(contractName, nameof(contractName)),
            contractId)
    {
        ControlIdentity = RequireValue(controlIdentity, nameof(controlIdentity));
        Family = RequireValue(family, nameof(family));
        Membership = membership;
        State = state;
        ValidatedState = validatedState;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public ArchitectureApplicabilityDiagnostic(
        string controlIdentity,
        string family,
        ArchitectureApplicabilityMembership? membership,
        ArchitectureApplicabilityRecordState? state,
        ArchitectureApplicabilityRecordState? validatedState,
        ArchitectureApplicabilityReason reason)
        : this(
            "applicability",
            RequireReason(reason).Provenance.PolicyIdentity is { Length: > 0 } policyIdentity ? policyIdentity : null,
            controlIdentity,
            family,
            membership,
            state,
            validatedState,
            RequireReason(reason),
            RequireReason(reason).Provenance)
    {
    }

    public string ControlIdentity { get; }

    public string EffectiveControlId => ControlIdentity;

    public string Family { get; }

    public ArchitectureApplicabilityMembership? Membership { get; }

    /// <summary>The raw record state, when a produced record was available.</summary>
    public ArchitectureApplicabilityRecordState? State { get; }

    /// <summary>The state after the evaluator's integrity checks; null means it is untrusted.</summary>
    public ArchitectureApplicabilityRecordState? ValidatedState { get; }

    public ArchitectureApplicabilityReason Reason { get; }

    public string ReasonCode => Reason.Code;

    public ArchitectureApplicabilityProvenance Provenance { get; }

    public string PolicyIdentity => Provenance.PolicyIdentity;

    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Applicability;

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A canonical applicability value is required.", parameterName)
            : value;

    private static ArchitectureApplicabilityReason RequireReason(ArchitectureApplicabilityReason? reason) =>
        reason ?? throw new ArgumentNullException(nameof(reason));
}

/// <summary>Shared summary, control evidence, and normalized findings for one assessment.</summary>
public sealed record ArchitectureApplicabilityProjection
{
    public ArchitectureApplicabilityProjection(
        ArchitectureAssessmentCompletionEvidence completion,
        ArchitectureApplicabilitySummary summary,
        IReadOnlyList<ArchitectureFinding> findings)
    {
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Controls = completion.Controls;
        Reasons = completion.Reasons;
        Findings = findings?.ToArray() ?? throw new ArgumentNullException(nameof(findings));
    }

    public ArchitectureAssessmentCompletionEvidence Completion { get; }

    public ArchitectureAssessmentCompletionEvidence CompletionEvidence => Completion;

    public ArchitectureApplicabilitySummary Summary { get; }

    public IReadOnlyList<ArchitectureApplicabilityAssessment> Controls { get; }

    public IReadOnlyList<ArchitectureApplicabilityReason> Reasons { get; }

    public IReadOnlyList<ArchitectureFinding> Findings { get; }
}
