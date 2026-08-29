using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationResult
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    public bool Passed { get; }
    public IReadOnlyCollection<ArchitectureViolation> Violations { get; }
    public IReadOnlyCollection<ArchitectureFinding> Findings { get; }
    public IReadOnlyCollection<string> Cycles { get; }
    public IReadOnlyCollection<ArchitectureCycleFinding> CycleFindings { get; }
    public IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings { get; }
    public string PolicyConsistencyConfig { get; }
    public IReadOnlyCollection<ArchitectureViolation> CoverageFindings { get; }
    public string CoverageConfig { get; }
    public IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }
    public string UnmatchedIgnoredViolationsConfig { get; }
    public IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries { get; }
    public ValidationTiming? Timing { get; }
    public IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics { get; }
    public bool PreflightBlocked { get; }
    public string? Mode { get; }
    public IReadOnlyCollection<BaselineLifecycleEntry> BaselineLifecycleEntries { get; }
    public IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation { get; }
    public IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> Waivers { get; }
    public ArchitectureAssessmentCompletionEvidence? AssessmentCompletionEvidence { get; }

    // Null unless the builder called WithProfile() — see
    // openspec/specs/analysis-profile/spec.md, "Testing API exposes the same profile semantics as
    // the CLI".
    public AnalysisProfile? Profile { get; }

    public ArchitectureValidationResult(ArchitectureValidationResultParams @params)
    {
        Passed = @params.Passed;
        Violations = @params.Violations;
        Cycles = @params.Cycles;
        CycleFindings = @params.CycleFindings ?? Array.Empty<ArchitectureCycleFinding>();
        PolicyConsistencyFindings = @params.PolicyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>();
        PolicyConsistencyConfig = @params.PolicyConsistencyConfig;
        CoverageFindings = @params.CoverageFindings ?? Array.Empty<ArchitectureViolation>();
        CoverageConfig = @params.CoverageConfig;
        UnmatchedIgnoredViolations = @params.UnmatchedIgnoredViolations ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        UnmatchedIgnoredViolationsConfig = @params.UnmatchedIgnoredViolationsConfig;
        CoverageSummaries = @params.CoverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>();
        Timing = @params.Timing;
        PreflightDiagnostics = @params.PreflightDiagnostics ?? Array.Empty<BuildStatePreflightDiagnostic>();
        PreflightBlocked = @params.PreflightBlocked;
        Mode = @params.Mode;
        BaselineLifecycleEntries = @params.BaselineLifecycleEntries ?? Array.Empty<BaselineLifecycleEntry>();
        SubtractiveMatcherParticipation = @params.SubtractiveMatcherParticipation
            ?? Array.Empty<ArchitectureSubtractiveMatcherParticipation>();
        Waivers = @params.Waivers ?? Array.Empty<ArchitectureWaiverLifecycleRecord>();
        AssessmentCompletionEvidence = @params.AssessmentCompletionEvidence;
        Profile = @params.Profile;
        Findings = ArchitectureFindingMapper.Order(AllDiagnostics());
    }

    private IEnumerable<ArchitectureFinding> AllDiagnostics()
    {
        foreach (ArchitectureFinding finding in ArchitectureFindingMapper.FromViolations(
                     Violations.Concat(CoverageFindings),
                     Mode))
        {
            yield return finding;
        }

        if (CycleFindings.Count > 0)
        {
            foreach (ArchitectureCycleFinding cycle in CycleFindings)
            {
                yield return ArchitectureFindingMapper.FromDiagnostic(ArchitectureDiagnosticMapper.FromCycle(cycle), Mode);
            }
        }
        else
        {
            foreach (string cycle in Cycles)
            {
                yield return ArchitectureFindingMapper.FromDiagnostic(
                    ArchitectureDiagnosticMapper.FromCycle(cycle, string.Empty, null),
                    Mode);
            }
        }

        foreach (PolicyConsistencyDiagnostic finding in PolicyConsistencyFindings)
        {
            yield return ArchitectureFindingMapper.FromDiagnostic(finding, Mode);
        }

        foreach (ArchitectureUnmatchedIgnoredViolation unmatched in UnmatchedIgnoredViolations)
        {
            yield return ArchitectureFindingMapper.FromDiagnostic(
                ArchitectureDiagnosticMapper.FromUnmatchedIgnore(unmatched),
                Mode);
        }

        foreach (BuildStatePreflightDiagnostic preflight in PreflightDiagnostics)
        {
            yield return ArchitectureFindingMapper.FromDiagnostic(preflight, Mode);
        }

        foreach (BaselineLifecycleEntry baseline in BaselineLifecycleEntries)
        {
            yield return ArchitectureFindingMapper.FromBaseline(baseline);
        }
    }

    public void ShouldPass()
    {
        if (Passed)
        {
            return;
        }

        throw new InvalidOperationException(BuildFailureMessage());
    }

    private string BuildFailureMessage()
    {
        string message = $"Architecture validation failed.{Environment.NewLine}";

        message += FormatFailureSection(
            null,
            PreflightDiagnostics.Count > 0 ? _formatter.FormatBuildStatePreflightForHumans(PreflightDiagnostics) : string.Empty);

        message += FormatFailureSection(
            "Violations:", Violations.Count > 0 ? _formatter.FormatViolationsForHumans(Violations) : string.Empty);

        message += FormatFailureSection(
            "Cycles:",
            BuildCycleFailureDetails());

        message += FormatFailureSection(
            null,
            PolicyConsistencyFindings.Count > 0
                ? _formatter.FormatPolicyConsistencyForHumans(PolicyConsistencyFindings)
                : string.Empty);

        message += FormatFailureSection(
            null, CoverageFindings.Count > 0 ? _formatter.FormatCoverageForHumans(CoverageFindings) : string.Empty);

        message += FormatFailureSection(
            null,
            UnmatchedIgnoredViolations.Count > 0
                ? _formatter.FormatUnmatchedForHumans(UnmatchedIgnoredViolations)
                : string.Empty);

        message += FormatFailureSection(
            null,
            Waivers.Count > 0 ? _formatter.FormatWaiversForHumans(Waivers) : string.Empty);

        message += FormatFailureSection(
            null,
            AssessmentCompletionEvidence?.State == ArchitectureAssessmentCompletionState.Unassessable
                ? ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(AssessmentCompletionEvidence)
                : string.Empty);

        return message;
    }

    private static string FormatFailureSection(string? label, string details)
    {
        if (string.IsNullOrEmpty(details))
        {
            return string.Empty;
        }

        return label is null
            ? $"{details}{Environment.NewLine}"
            : $"{label}{Environment.NewLine}{details}{Environment.NewLine}";
    }

    private string BuildCycleFailureDetails()
    {
        if (CycleFindings.Count > 0)
        {
            return ArchitectureDiagnosticFormatter.FormatCyclesForHumans(CycleFindings);
        }

        return Cycles.Count > 0 ? _formatter.FormatCyclesForHumans(Cycles) : string.Empty;
    }
}

public sealed record ArchitectureValidationResultParams(
    bool Passed,
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles,
    IReadOnlyCollection<PolicyConsistencyDiagnostic>? PolicyConsistencyFindings = null,
    string PolicyConsistencyConfig = "error",
    IReadOnlyCollection<ArchitectureViolation>? CoverageFindings = null,
    string CoverageConfig = "off",
    IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? UnmatchedIgnoredViolations = null,
    string UnmatchedIgnoredViolationsConfig = "off",
    IReadOnlyCollection<ArchitectureCoverageSummary>? CoverageSummaries = null,
    ValidationTiming? Timing = null)
{
    public IReadOnlyCollection<ArchitectureCycleFinding>? CycleFindings { get; init; }
    public IReadOnlyCollection<BuildStatePreflightDiagnostic>? PreflightDiagnostics { get; init; }
    public bool PreflightBlocked { get; init; }
    public string? Mode { get; init; }
    public IReadOnlyCollection<BaselineLifecycleEntry>? BaselineLifecycleEntries { get; init; }
    public IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? SubtractiveMatcherParticipation { get; init; }
    public IReadOnlyCollection<ArchitectureWaiverLifecycleRecord>? Waivers { get; init; }
    public AnalysisProfile? Profile { get; init; }
    public ArchitectureAssessmentCompletionEvidence? AssessmentCompletionEvidence { get; init; }
}
