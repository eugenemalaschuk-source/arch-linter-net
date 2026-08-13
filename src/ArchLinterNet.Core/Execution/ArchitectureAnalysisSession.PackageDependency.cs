using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckPackageDependencyContract(ArchitecturePackageDependencyContract contract)
    {
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = PackageDependencyChecker.Check(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckPackageAllowOnlyContract(ArchitecturePackageAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations =
            PackageDependencyChecker.CheckAllowOnly(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
