using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution.Abstractions;

public sealed record ArchitectureHandlerResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles)
{
    public static ArchitectureHandlerResult FromViolations(IReadOnlyCollection<ArchitectureViolation> violations) =>
        new(violations, Array.Empty<string>());

    public static ArchitectureHandlerResult FromCycles(IReadOnlyCollection<string> cycles) =>
        new(Array.Empty<ArchitectureViolation>(), cycles);
}

public delegate ArchitectureHandlerResult ArchitectureContractChecker(
    ArchitectureAnalysisSession session, IArchitectureContract contract);
