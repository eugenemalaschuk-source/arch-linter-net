using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Session-side entry points for the two contextual contract families (context_dependencies,
// context_allow_only), plus the contract-shape registration of their consumers, which is session
// state rather than checking. The families' own scanning lives in ContextDependencyChecker /
// ContextAllowOnlyChecker.
public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckContextDependencyContract(ArchitectureContextDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = ContextDependencyChecker.Check(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckContextAllowOnlyContract(ArchitectureContextAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = ContextAllowOnlyChecker.Check(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private void RegisterContextualConsumers(
        ArchitectureContextSelector source,
        IEnumerable<ArchitectureContextSelector> targetSelectors,
        IEnumerable<ArchitectureContextSelector> excludeSelectors)
    {
        RegisterContextualConsumer(source);

        foreach (ArchitectureContextSelector selector in targetSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }

        foreach (ArchitectureContextSelector selector in excludeSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }
    }
}
