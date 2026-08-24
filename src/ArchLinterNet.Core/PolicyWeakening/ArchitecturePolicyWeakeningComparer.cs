using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Compares effective policy contexts without loading policy files or evaluating architecture.</summary>
public static class ArchitecturePolicyWeakeningComparer
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    /// <summary>Compares separately loaded base and current effective policy contexts.</summary>
    public static ArchitecturePolicyWeakeningResult Compare(ArchitecturePolicyWeakeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseContext);
        ArgumentNullException.ThrowIfNull(request.CurrentContext);
        ArchitecturePolicyWeakeningContextSupport.ValidateComparableContexts(request.BaseContext, request.CurrentContext);

        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> baseStrict = ContractMap(request.BaseContext, "strict", "base");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentStrict = ContractMap(request.CurrentContext, "strict", "current");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentAudit = ControlMap(request.CurrentContext, "audit");
        List<ArchitecturePolicyWeakeningFinding> findings = new();

        ArchitecturePolicyWeakeningEnforcementEvaluator.Evaluate(
            baseStrict,
            currentStrict,
            currentAudit,
            request.CurrentContext.Guardrails.PolicyWeakening,
            findings);
        ArchitecturePolicyWeakeningAnalysisScopeEvaluator.Evaluate(request.BaseContext, request.CurrentContext, findings);
        ArchitecturePolicyWeakeningStaticScopeEvaluator.Evaluate(request.BaseContext, request.CurrentContext, findings);
        ArchitecturePolicyWeakeningContractFactsEvaluator.Evaluate(
            request.BaseContext,
            request.CurrentContext,
            request.CurrentContext.Guardrails.PolicyWeakening,
            findings);
        ArchitecturePolicyWeakeningExceptionEvaluator.Evaluate(
            request.BaseContext,
            request.CurrentContext,
            request.CurrentContext.Guardrails.PolicyWeakening,
            findings);
        ArchitecturePolicyWeakeningSelectorEvaluator.Evaluate(request, findings);

        return new ArchitecturePolicyWeakeningResult(
            ArchitecturePolicyWeakeningResult.CurrentSchemaVersion,
            ArchitecturePolicyWeakeningResult.ResultKind,
            request.CurrentContext.Policy.Name,
            request.CurrentContext.Policy.Version,
            request.CurrentContext.Guardrails.PolicyWeakening,
            findings
                .GroupBy(finding => finding.Identity, _comparer)
                .Select(group => group.First())
                .OrderBy(finding => finding.Kind, _comparer)
                .ThenBy(finding => finding.ControlIdentity, _comparer)
                .ThenBy(finding => finding.Identity, _comparer)
                .ToArray());
    }
}
