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
            IncludeAsmdefContracts = false,
        };

        ValidationOutcome outcome = ArchitectureValidationService.Validate(request);

        violations = outcome.Violations;
        cycles = outcome.Cycles;
        return outcome.Passed;
    }
}
