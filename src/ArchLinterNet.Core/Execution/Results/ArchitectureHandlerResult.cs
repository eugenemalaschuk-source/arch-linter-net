using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution.Results;

/// <summary>
/// The result produced by a contract-family handler.
/// </summary>
public sealed record ArchitectureHandlerResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles)
{
    public static ArchitectureHandlerResult FromViolations(IReadOnlyCollection<ArchitectureViolation> violations) =>
        new(violations, Array.Empty<string>());

    public static ArchitectureHandlerResult FromCycles(IReadOnlyCollection<string> cycles) =>
        new(Array.Empty<ArchitectureViolation>(), cycles);
}
