using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core;

public sealed class ArchitectureValidator
{
    public bool Validate(string policyPath)
    {
        return Validate(policyPath, out _, out _);
    }

    public bool Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return Validate(policyPath, out violations, out _);
    }

    public bool Validate(
        string policyPath,
        out IReadOnlyCollection<ArchitectureViolation> violations,
        out IReadOnlyCollection<string> cycles,
        IReadOnlyList<string>? preprocessorSymbols = null)
    {
        ValidationRequest request = new()
        {
            PolicyPath = policyPath,
            Mode = "strict",
            PreprocessorSymbols = preprocessorSymbols,
        };

        ValidationOutcome outcome = ArchitectureValidationService.Validate(request);

        violations = outcome.PolicyConsistencyConfig == "off"
            ? outcome.Violations
                .Concat(outcome.CoverageConfig == "off" ? Array.Empty<ArchitectureViolation>() : outcome.CoverageFindings)
                .ToArray()
            : outcome.Violations
                .Concat(outcome.CoverageConfig == "off" ? Array.Empty<ArchitectureViolation>() : outcome.CoverageFindings)
                .Concat(outcome.PolicyConsistencyFindings.Select(ToViolation))
                .ToArray();
        cycles = outcome.Cycles;
        return outcome.Passed;
    }

    private static ArchitectureViolation ToViolation(PolicyConsistencyDiagnostic finding)
    {
        return new ArchitectureViolation(
            finding.ContractName,
            finding.ContractId,
            finding.CheckKind,
            finding.Reason,
            finding.ConflictingContractNames);
    }
}
