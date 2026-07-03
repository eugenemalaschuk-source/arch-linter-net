using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Unity;

public sealed class AsmdefValidator
{
    private static readonly Lazy<ArchitectureEngine> _defaultEngine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public bool Validate(string contractPath)
    {
        return Validate(contractPath, out _);
    }

    public bool Validate(string contractPath, out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        AsmdefValidationOutcome outcome = _defaultEngine.Value.ValidateAsmdef(new AsmdefValidationRequest
        {
            PolicyPath = contractPath,
        });

        violations = outcome.Violations;
        return outcome.Passed;
    }
}
