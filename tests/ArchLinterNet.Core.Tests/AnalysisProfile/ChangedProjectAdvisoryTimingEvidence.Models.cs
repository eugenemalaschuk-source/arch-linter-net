using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal sealed record ChangedProjectAdvisoryTimingEvidenceDocument
{
    public const string SchemaId = "changed-project-advisory-timing-evidence/v1";

    public required string EvidenceSchemaId { get; init; }

    public required string Issue { get; init; }

    public required string Outcome { get; init; }

    public required string SourceIdentity { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required string Configuration { get; init; }

    public required string MeasurementBoundary { get; init; }

    public required string DecisionNote { get; init; }

    public required IReadOnlyList<ChangedProjectAdvisoryScaleEvidence> ScalePoints { get; init; }

    public void Validate()
    {
        if (!string.Equals(EvidenceSchemaId, SchemaId, StringComparison.Ordinal) ||
            !string.Equals(Issue, "#503", StringComparison.Ordinal) ||
            !string.Equals(Outcome, "C", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Changed-project timing evidence has an invalid identity or outcome.");
        }

        if (ScalePoints.Count < 3)
        {
            throw new InvalidOperationException("Changed-project timing evidence requires S/M/L scale points.");
        }

        foreach (ChangedProjectAdvisoryScaleEvidence scale in ScalePoints)
        {
            scale.Validate();
        }
    }
}

internal sealed record ChangedProjectAdvisoryScaleEvidence
{
    public required string Label { get; init; }

    public required int ProjectCount { get; init; }

    public required int FullProjectCount { get; init; }

    public required int ScopePlanningSampleCount { get; init; }

    public required decimal ScopePlanningMedianMilliseconds { get; init; }

    public required decimal FullValidationMedianMilliseconds { get; init; }

    public required decimal FixedPhaseMedianMilliseconds { get; init; }

    public required decimal ProjectDependentPhaseMedianMilliseconds { get; init; }

    public required IReadOnlyList<ChangedProjectAdvisoryEstimate> Estimates { get; init; }

    public required IReadOnlyList<ChangedProjectAdvisoryTimingSample> Samples { get; init; }

    public void Validate()
    {
        if (ProjectCount != FullProjectCount || ProjectCount < 1 ||
            ScopePlanningSampleCount < 1 || ScopePlanningMedianMilliseconds < 0 ||
            FullValidationMedianMilliseconds <= 0 || FixedPhaseMedianMilliseconds < 0 ||
            ProjectDependentPhaseMedianMilliseconds < 0 || Samples.Count < 3)
        {
            throw new InvalidOperationException($"Invalid timing evidence scale point '{Label}'.");
        }

        if (Math.Abs(
                FullValidationMedianMilliseconds -
                (FixedPhaseMedianMilliseconds + ProjectDependentPhaseMedianMilliseconds)) > 0.01m)
        {
            throw new InvalidOperationException($"Scale point '{Label}' does not conserve its timing decomposition.");
        }

        foreach (ChangedProjectAdvisoryEstimate estimate in Estimates)
        {
            estimate.Validate(ProjectCount, FullValidationMedianMilliseconds, FixedPhaseMedianMilliseconds,
                ProjectDependentPhaseMedianMilliseconds, ScopePlanningMedianMilliseconds);
        }

        foreach (ChangedProjectAdvisoryTimingSample sample in Samples)
        {
            sample.Validate(ProjectCount);
        }
    }
}

internal sealed record ChangedProjectAdvisoryEstimate
{
    public required string ChangeClass { get; init; }

    public required string Disposition { get; init; }

    public required int AffectedProjectCount { get; init; }

    public required decimal AffectedScopeRatio { get; init; }

    public required decimal ModeledAdvisoryMilliseconds { get; init; }

    public required decimal ModeledReductionPercent { get; init; }

    public required string Authority { get; init; }

    public void Validate(
        int projectCount,
        decimal fullValidationMilliseconds,
        decimal fixedPhaseMilliseconds,
        decimal projectDependentMilliseconds,
        decimal planningMilliseconds)
    {
        if (AffectedProjectCount is < 1 or > 10_000 || AffectedScopeRatio is < 0 or > 1 ||
            ModeledAdvisoryMilliseconds < 0 || string.IsNullOrWhiteSpace(Authority) ||
            !string.Equals(Disposition, "modeled-only", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid timing estimate for '{ChangeClass}'.");
        }

        decimal expectedRatio = (decimal)AffectedProjectCount / projectCount;
        if (Math.Abs(AffectedScopeRatio - expectedRatio) > 0.0005m)
        {
            throw new InvalidOperationException($"Estimate '{ChangeClass}' has an invalid K/P ratio.");
        }

        decimal expectedMilliseconds = fixedPhaseMilliseconds +
            (projectDependentMilliseconds * AffectedScopeRatio) + planningMilliseconds;
        if (Math.Abs(ModeledAdvisoryMilliseconds - expectedMilliseconds) > 0.01m)
        {
            throw new InvalidOperationException($"Estimate '{ChangeClass}' has an invalid modeled duration.");
        }

        decimal expectedReduction = fullValidationMilliseconds > 0
            ? Math.Clamp((1 - (ModeledAdvisoryMilliseconds / fullValidationMilliseconds)) * 100, -100, 100)
            : 0;
        if (Math.Abs(ModeledReductionPercent - expectedReduction) > 0.01m)
        {
            throw new InvalidOperationException($"Estimate '{ChangeClass}' has an invalid reduction percentage.");
        }
    }
}

internal sealed record ChangedProjectAdvisoryTimingSample
{
    public required int SampleOrdinal { get; init; }

    public required string CompletionStatus { get; init; }

    public required int ExitCode { get; init; }

    public required bool OutputFailed { get; init; }

    public required string CanonicalResultSha256 { get; init; }

    public required IReadOnlyDictionary<string, int> Counters { get; init; }

    public required IReadOnlyDictionary<string, decimal> TopLevelPhaseMilliseconds { get; init; }

    public required JsonElement RawAnalysisProfile { get; init; }

    public void Validate(int projectCount)
    {
        if (SampleOrdinal < 1 || CompletionStatus != "Success" || ExitCode != 0 || OutputFailed ||
            string.IsNullOrWhiteSpace(CanonicalResultSha256) ||
            Counters.GetValueOrDefault("discovered_project_count") != projectCount &&
            Counters.GetValueOrDefault("selected_assembly_count") != projectCount &&
            Counters.GetValueOrDefault("retained_assembly_count") != projectCount ||
            !RawAnalysisProfile.TryGetProperty("SchemaId", out JsonElement schemaId) ||
            schemaId.GetString() != "analysis-profile/v1")
        {
            throw new InvalidOperationException($"Invalid successful timing sample {SampleOrdinal}.");
        }

        if (TopLevelPhaseMilliseconds.Count == 0 ||
            TopLevelPhaseMilliseconds.Values.Any(value => value < 0))
        {
            throw new InvalidOperationException($"Timing sample {SampleOrdinal} has no valid phase evidence.");
        }
    }
}
