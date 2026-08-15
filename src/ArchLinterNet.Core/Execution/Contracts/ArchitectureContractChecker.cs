using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Results;

namespace ArchLinterNet.Core.Execution.Contracts;

/// <summary>
/// Evaluates a concrete policy contract within an analysis session.
/// </summary>
public delegate ArchitectureHandlerResult ArchitectureContractChecker(
    ArchitectureAnalysisSession session, IArchitectureContract contract);
