using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationResult
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    public bool Passed { get; }
    public IReadOnlyCollection<ArchitectureViolation> Violations { get; }
    public IReadOnlyCollection<string> Cycles { get; }
    public IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings { get; }
    public string PolicyConsistencyConfig { get; }
    public IReadOnlyCollection<ArchitectureViolation> CoverageFindings { get; }
    public string CoverageConfig { get; }
    public IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }
    public string UnmatchedIgnoredViolationsConfig { get; }
    public IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries { get; }
    public ValidationTiming? Timing { get; }

    public ArchitectureValidationResult(
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        string policyConsistencyConfig = "error",
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        string coverageConfig = "off",
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatchedIgnoredViolations = null,
        string unmatchedIgnoredViolationsConfig = "off",
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        ValidationTiming? timing = null)
    {
        Passed = passed;
        Violations = violations;
        Cycles = cycles;
        PolicyConsistencyFindings = policyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>();
        PolicyConsistencyConfig = policyConsistencyConfig;
        CoverageFindings = coverageFindings ?? Array.Empty<ArchitectureViolation>();
        CoverageConfig = coverageConfig;
        UnmatchedIgnoredViolations = unmatchedIgnoredViolations ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        UnmatchedIgnoredViolationsConfig = unmatchedIgnoredViolationsConfig;
        CoverageSummaries = coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>();
        Timing = timing;
    }

    public void ShouldPass()
    {
        if (!Passed)
        {
            string violationDetails = Violations.Count > 0
                ? _formatter.FormatViolationsForHumans(Violations)
                : string.Empty;

            string cycleDetails = Cycles.Count > 0
                ? _formatter.FormatCyclesForHumans(Cycles)
                : string.Empty;

            string policyConsistencyDetails = PolicyConsistencyFindings.Count > 0
                ? _formatter.FormatPolicyConsistencyForHumans(PolicyConsistencyFindings)
                : string.Empty;

            string coverageDetails = CoverageFindings.Count > 0
                ? _formatter.FormatCoverageForHumans(CoverageFindings)
                : string.Empty;

            string unmatchedIgnoredDetails = UnmatchedIgnoredViolations.Count > 0
                ? _formatter.FormatUnmatchedForHumans(UnmatchedIgnoredViolations)
                : string.Empty;

            string message = $"Architecture validation failed.{Environment.NewLine}";
            if (!string.IsNullOrEmpty(violationDetails))
            {
                message += $"Violations:{Environment.NewLine}{violationDetails}{Environment.NewLine}";
            }

            if (!string.IsNullOrEmpty(cycleDetails))
            {
                message += $"Cycles:{Environment.NewLine}{cycleDetails}{Environment.NewLine}";
            }

            if (!string.IsNullOrEmpty(policyConsistencyDetails))
            {
                message += $"{policyConsistencyDetails}{Environment.NewLine}";
            }

            if (!string.IsNullOrEmpty(coverageDetails))
            {
                message += $"{coverageDetails}{Environment.NewLine}";
            }

            if (!string.IsNullOrEmpty(unmatchedIgnoredDetails))
            {
                message += $"{unmatchedIgnoredDetails}{Environment.NewLine}";
            }

            throw new InvalidOperationException(message);
        }
    }
}
