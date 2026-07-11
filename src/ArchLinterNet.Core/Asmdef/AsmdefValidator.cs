using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Asmdef;

public static class AsmdefValidator
{
    private static readonly Lazy<ArchitectureEngine> _defaultEngine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static bool Validate(string policyPath)
    {
        return Validate(policyPath, out _);
    }

    public static bool Validate(
        string policyPath,
        out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        AsmdefValidationOutcome outcome = _defaultEngine.Value.ValidateAsmdef(new AsmdefValidationRequest
        {
            PolicyPath = policyPath,
        });

        violations = outcome.Violations;
        return outcome.Passed;
    }
}
