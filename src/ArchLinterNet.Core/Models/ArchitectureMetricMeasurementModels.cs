namespace ArchLinterNet.Core.Model;

/// <summary>One deterministic metric result and its shared applicability state.</summary>
public sealed record ArchitectureMetricMeasurement(
    string Id,
    string Kind,
    string? NativeSubject,
    string? Unit,
    string EffectiveScope,
    ArchitectureApplicabilityRecordState State,
    int? Value,
    IReadOnlyList<string> Contributors)
{
    public IReadOnlyList<string> Contributors { get; init; } = Contributors
        .OrderBy(contributor => contributor, StringComparer.Ordinal)
        .ToArray();

    public int ContributorCount => Value is null ? 0 : Contributors.Count;

    public bool IsEvaluable => State == ArchitectureApplicabilityRecordState.Evaluable && Value is not null;

    public bool IsUnassessable => State == ArchitectureApplicabilityRecordState.Unassessable || Value is null;
}

/// <summary>Reusable Core outcome for a selected set of policy metric definitions.</summary>
public sealed record ArchitectureMetricMeasurementOutcome(
    IReadOnlyList<ArchitectureMetricMeasurement> Measurements,
    ArchitectureAssessmentCompletionEvidence? Completion,
    ArchitectureApplicabilityProjection? Applicability)
{
    public IReadOnlyList<ArchitectureMetricMeasurement> Results => Measurements;

    public ArchitectureAssessmentCompletionEvidence? AssessmentCompletionEvidence => Completion;

    public ArchitectureApplicabilityProjection? ApplicabilityProjection => Applicability;

    public bool IsComplete => Measurements.All(measurement => measurement.IsEvaluable)
        && (Completion is null || Completion.State == ArchitectureAssessmentCompletionState.Pass);

    public IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpectedEntries =>
        Completion?.Controls
            .Where(control => control.Expected is not null)
            .Select(control => control.Expected!)
            .ToArray()
        ?? Array.Empty<ArchitectureApplicabilityExpectedEntry>();

    public IReadOnlyList<ArchitectureApplicabilityRecord> ApplicabilityRecords =>
        Completion?.Controls
            .Where(control => control.Record is not null)
            .Select(control => control.Record!)
            .ToArray()
        ?? Array.Empty<ArchitectureApplicabilityRecord>();
}
