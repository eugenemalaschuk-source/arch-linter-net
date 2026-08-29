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

    // Applicability is opt-in. Existing families leave both collections empty, preserving the
    // pre-v0.8 execution shape while giving future family handlers one canonical transport
    // boundary for expected membership and produced evidence.
    public IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpectedEntries { get; init; } =
        Array.Empty<ArchitectureApplicabilityExpectedEntry>();

    public IReadOnlyList<ArchitectureApplicabilityRecord> ApplicabilityRecords { get; init; } =
        Array.Empty<ArchitectureApplicabilityRecord>();
}
