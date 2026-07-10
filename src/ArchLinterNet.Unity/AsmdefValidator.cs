using ArchLinterNet.Core.Asmdef;
using ArchLinterNet.Core.Composition;

namespace ArchLinterNet.Unity;

public static class AsmdefValidator
{
    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static bool Validate(string contractPath)
    {
        return Validate(contractPath, out _);
    }

    public static bool Validate(string contractPath, out IReadOnlyCollection<Core.Model.ArchitectureViolation> violations)
    {
        AsmdefValidationOutcome outcome = _engine.Value.ValidateAsmdef(new AsmdefValidationRequest
        {
            PolicyPath = contractPath,
        });

        violations = outcome.Violations;
        return outcome.Passed;
    }
}
