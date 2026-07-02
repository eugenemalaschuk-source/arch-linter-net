using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed record ArchitectureHandlerResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles)
{
    public static ArchitectureHandlerResult FromViolations(IReadOnlyCollection<ArchitectureViolation> violations) =>
        new(violations, Array.Empty<string>());

    public static ArchitectureHandlerResult FromCycles(IReadOnlyCollection<string> cycles) =>
        new(Array.Empty<ArchitectureViolation>(), cycles);
}

public interface IArchitectureContractHandler
{
    string Family { get; }

    ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract);
}
