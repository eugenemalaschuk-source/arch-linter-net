using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution.Abstractions;

public sealed record ArchitectureContractExecutionResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles,
    IReadOnlyCollection<ArchitectureViolation> CoverageViolations,
    IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries);

public interface IArchitectureContractExecutor
{
    ArchitectureContractExecutionResult Execute(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        bool includeAsmdefContracts = true,
        ValidationTiming? timing = null);
}
