using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

internal sealed record ArchitectureHandlerResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles)
{
    public static ArchitectureHandlerResult FromViolations(IReadOnlyCollection<ArchitectureViolation> violations) =>
        new(violations, Array.Empty<string>());

    public static ArchitectureHandlerResult FromCycles(IReadOnlyCollection<string> cycles) =>
        new(Array.Empty<ArchitectureViolation>(), cycles);
}

internal interface IArchitectureContractHandler
{
    string Family { get; }

    ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract);
}
