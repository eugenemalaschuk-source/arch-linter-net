using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationBuilder
{
    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

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

        ValidationOutcome outcome = _engine.Value.Validate(request);

        return new ArchitectureValidationResult(
            outcome.Passed,
            outcome.Violations,
            outcome.Cycles,
            outcome.PolicyConsistencyFindings,
            outcome.PolicyConsistencyConfig);
    }
}
