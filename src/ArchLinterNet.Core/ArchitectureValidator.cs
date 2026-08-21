using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core;

public static class ArchitectureValidator
{
    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        return _engine.Value.Validate(request, timing);
    }

    public static bool Validate(string policyPath)
    {
        return Validate(policyPath, out _, out _);
    }

    public static bool Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return Validate(policyPath, out violations, out _);
    }

    public static bool Validate(
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

        ValidationOutcome outcome = _engine.Value.Validate(request);

        IReadOnlyCollection<ArchitectureViolation> coverageViolations = outcome.CoverageConfig == "off"
            ? Array.Empty<ArchitectureViolation>()
            : outcome.CoverageFindings;

        violations = outcome.PolicyConsistencyConfig == "off"
            ? outcome.Violations.Concat(coverageViolations).ToArray()
            : outcome.Violations
                .Concat(coverageViolations)
                .Concat(outcome.PolicyConsistencyFindings.Select(ToViolation))
                .ToArray();
        cycles = outcome.Cycles;
        return outcome.Passed;
    }

    /// <summary>Validates policy and static configuration without loading target assemblies.</summary>
    public static PolicyCheckOutcome CheckPolicy(string policyPath)
    {
        return _engine.Value.CheckPolicy(policyPath);
    }

    /// <summary>Exports effective-policy facts without project or assembly analysis.</summary>
    public static ArchitecturePolicyContextExport ExportPolicyContext(string policyPath)
    {
        return _engine.Value.ExportPolicyContext(new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
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
