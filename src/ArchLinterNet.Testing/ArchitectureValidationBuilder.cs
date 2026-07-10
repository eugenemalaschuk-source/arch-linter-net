using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationBuilder
{
    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    private readonly string _policyPath;
    private string? _conditionSetName;
    private IReadOnlyCollection<string>? _contractIds;
    private string? _baselinePath;
    private bool _enforceUnmatchedIgnoredViolationsPolicy;
    private bool _collectTimings;

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
    }

    public ArchitectureValidationBuilder WithConditionSet(string name)
    {
        _conditionSetName = name;
        return this;
    }

    public ArchitectureValidationBuilder WithContracts(IEnumerable<string> contractIds)
    {
        _contractIds = contractIds.ToArray();
        return this;
    }

    public ArchitectureValidationBuilder WithContracts(params string[] contractIds)
    {
        return WithContracts((IEnumerable<string>)contractIds);
    }

    public ArchitectureValidationBuilder WithBaseline(string baselinePath)
    {
        _baselinePath = baselinePath;
        return this;
    }

    public ArchitectureValidationBuilder WithUnmatchedIgnoredViolationsPolicy(bool enforce = true)
    {
        _enforceUnmatchedIgnoredViolationsPolicy = enforce;
        return this;
    }

    public ArchitectureValidationBuilder WithTimings()
    {
        _collectTimings = true;
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
            ContractIds = _contractIds,
            BaselinePath = _baselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = _enforceUnmatchedIgnoredViolationsPolicy,
        };

        ValidationTiming? timing = _collectTimings ? new ValidationTiming() : null;
        ValidationOutcome outcome = _engine.Value.Validate(request, timing);

        return new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            outcome.Passed,
            outcome.Violations,
            outcome.Cycles,
            outcome.PolicyConsistencyFindings,
            outcome.PolicyConsistencyConfig,
            outcome.CoverageFindings,
            outcome.CoverageConfig,
            outcome.UnmatchedIgnoredViolations,
            outcome.UnmatchedIgnoredViolationsConfig,
            outcome.CoverageSummaries,
            timing));
    }
}
