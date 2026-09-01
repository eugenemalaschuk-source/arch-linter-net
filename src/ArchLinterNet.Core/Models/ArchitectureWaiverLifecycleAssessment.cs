namespace ArchLinterNet.Core.Model;

/// <summary>
/// The already-evaluated waiver lifecycle result for one validation mode. The lifecycle profile
/// owns which record states are blocking, so consumers do not reconstruct that decision from
/// aggregate inventory counts.
/// </summary>
public sealed record ArchitectureWaiverLifecycleAssessment(
    string Profile,
    IReadOnlyList<ArchitectureWaiverLifecycleRecord> Records,
    IReadOnlyList<string> BlockingStates)
{
    public IReadOnlyList<ArchitectureWaiverLifecycleRecord> Records { get; init; } =
        (Records ?? throw new ArgumentNullException(nameof(Records)))
        .OrderBy(record => record.Id, StringComparer.Ordinal)
        .ThenBy(record => record.ContractGroup, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> BlockingStates { get; init; } =
        (BlockingStates ?? throw new ArgumentNullException(nameof(BlockingStates)))
        .OrderBy(state => state, StringComparer.Ordinal)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    /// <summary>Whether a lifecycle record is currently blocking under <see cref="Profile"/>.</summary>
    public bool HasBlockingRecords => Records.Any(record => BlockingStates.Contains(record.State, StringComparer.Ordinal));
}
