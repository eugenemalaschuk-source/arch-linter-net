namespace ArchLinterNet.Core.Contracts;

/// <summary>
/// Optional deterministic operation counts for focused baseline-merge evidence.
/// Production callers leave the counter null.
/// </summary>
internal sealed class ArchitectureBaselineMergeWorkCounter
{
    internal long ContractIndexEntries { get; set; }

    internal long ContractLookups { get; set; }

    internal long ContractLinearScanComparisons { get; set; }

    internal long InlineIgnoreIndexEntries { get; set; }

    internal long MembershipChecks { get; set; }

    internal long LoadedIgnoresAccepted { get; set; }
}
