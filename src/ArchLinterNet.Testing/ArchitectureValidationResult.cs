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

    public ArchitectureValidationResult(
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        string policyConsistencyConfig = "error")
    {
        Passed = passed;
        Violations = violations;
        Cycles = cycles;
        PolicyConsistencyFindings = policyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>();
        PolicyConsistencyConfig = policyConsistencyConfig;
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

            throw new InvalidOperationException(message);
        }
    }
}
