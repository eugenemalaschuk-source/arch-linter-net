using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckLayoutConventionsContract(ArchitectureLayoutConventionContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        LayoutConventionChecker.Result result = LayoutConventionChecker.Check(contract, CheckerContext, executionContext);
        if (result.EvaluatedIgnores)
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        }

        return result.Violations;
    }
}
