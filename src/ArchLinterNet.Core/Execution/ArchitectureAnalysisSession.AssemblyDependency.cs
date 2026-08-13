using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckAssemblyDependencyContract(ArchitectureAssemblyDependencyContract contract)
    {
        // Contract overload, not the id-only one: these families support source-set expansion, so
        // selecting the authored id must reach every expanded instance it produced (see
        // openspec/specs/source-set-expansion, "The authored id selects every instance"), exactly
        // as the package, framework-reference, and external families already do.
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = AssemblyDependencyChecker.Check(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckAssemblyAllowOnlyContract(ArchitectureAssemblyAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations =
            AssemblyDependencyChecker.CheckAllowOnly(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
