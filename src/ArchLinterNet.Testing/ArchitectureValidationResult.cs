using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationResult
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    public bool Passed { get; }
    public IReadOnlyCollection<ArchitectureViolation> Violations { get; }
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
}
