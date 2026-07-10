using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = AssemblyIndependenceChecker.Check(contract, Context.TargetAssemblies, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
