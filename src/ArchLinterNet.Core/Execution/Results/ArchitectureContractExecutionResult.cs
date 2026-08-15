using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution.Results;

/// <summary>
/// Aggregated results from evaluating the selected contract families.
/// </summary>
public sealed record ArchitectureContractExecutionResult(
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles,
    IReadOnlyCollection<ArchitectureViolation> CoverageViolations,
    IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries)
{
    public IReadOnlyCollection<ArchitectureCycleFinding> CycleFindings { get; init; } =
        Array.Empty<ArchitectureCycleFinding>();

    public IReadOnlyDictionary<string, int> ContractFamilyResultCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
