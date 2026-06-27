using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

public static class ArchitectureAssertions
{
    public static ArchitectureValidationBuilder FromPolicy(string policyPath)
    {
        return new ArchitectureValidationBuilder(policyPath);
    }

    public static ArchitectureValidationBuilder FromRepositoryRoot(string repositoryRoot)
    {
        string policyPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");
        return new ArchitectureValidationBuilder(policyPath);
    }
}

public sealed class ArchitectureValidationBuilder
{
    private readonly string _policyPath;
    private string? _conditionSetName;

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
    }

    public ArchitectureValidationBuilder WithConditionSet(string name)
    {
        _conditionSetName = name;
        return this;
    }

    public ArchitectureValidationResult ValidateStrict()
    {
        return Validate(mode: "strict");
    }

    public ArchitectureValidationResult ValidateAudit()
    {
        return Validate(mode: "audit");
    }

    private ArchitectureValidationResult Validate(string mode)
    {
        ValidationRequest request = new()
        {
            PolicyPath = _policyPath,
            Mode = mode,
            ConditionSetName = _conditionSetName,
        };

        ValidationOutcome outcome = ArchitectureValidationService.Validate(request);

        return new ArchitectureValidationResult(
            outcome.Passed,
            outcome.Violations,
            outcome.Cycles,
            outcome.PolicyConsistencyFindings,
            outcome.PolicyConsistencyConfig);
    }
}

public sealed class ArchitectureValidationResult
{
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
                ? ArchitectureDiagnosticFormatter.FormatViolationsForHumans(Violations)
                : string.Empty;

            string cycleDetails = Cycles.Count > 0
                ? ArchitectureDiagnosticFormatter.FormatCyclesForHumans(Cycles)
                : string.Empty;

            string policyConsistencyDetails = PolicyConsistencyFindings.Count > 0
                ? ArchitectureDiagnosticFormatter.FormatPolicyConsistencyForHumans(PolicyConsistencyFindings)
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
