using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;

namespace ArchLinterNet.Core.Execution.Abstractions;

public interface IArchitectureContractHandlerRegistry
{
    bool TryGetHandler(string family, out ArchitectureContractChecker? checker);

    ArchitectureHandlerResult Execute(string family, ArchitectureAnalysisSession session, IArchitectureContract contract);
}
