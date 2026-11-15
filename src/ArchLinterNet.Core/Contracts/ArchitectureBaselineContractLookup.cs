using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

/// <summary>Resolves baseline contract IDs while preserving the first case-insensitive match.</summary>
internal sealed class ArchitectureBaselineContractLookup
{
    private const int SingleIdLinearLookupEntryThreshold = 8;
    private const int SmallContractGroupLinearLookupThreshold = 4;

    private readonly List<IArchitectureContract> _contracts;
    private readonly bool _smallContractGroup;
    private readonly bool _singleIdGroup;
    private readonly ArchitectureBaselineMergeWorkCounter? _workCounter;
    private Dictionary<string, IArchitectureContract>? _contractsById;
    private IArchitectureContract? _singleIdContract;
    private bool _singleIdContractResolved;

    internal ArchitectureBaselineContractLookup(
        List<IArchitectureContract> contracts,
        List<ArchitectureBaselineContractEntry> baselineEntries,
        ArchitectureBaselineMergeWorkCounter? workCounter)
    {
        _contracts = contracts;
        _smallContractGroup = contracts.Count <= SmallContractGroupLinearLookupThreshold;
        _singleIdGroup = HasSingleContractId(baselineEntries);
        _workCounter = workCounter;
    }

    internal IArchitectureContract? Find(string contractId)
    {
        if (_workCounter is not null)
        {
            _workCounter.ContractLookups++;
        }

        if (!_singleIdGroup)
        {
            if (_smallContractGroup)
            {
                return FindFirst(contractId);
            }

            _contractsById ??= BuildIndex();
            _contractsById.TryGetValue(contractId, out IArchitectureContract? contract);
            return contract;
        }

        if (!_singleIdContractResolved)
        {
            _singleIdContract = FindFirst(contractId);
            _singleIdContractResolved = true;
        }

        return _singleIdContract;
    }

    private static bool HasSingleContractId(List<ArchitectureBaselineContractEntry> baselineEntries)
    {
        if (baselineEntries.Count > SingleIdLinearLookupEntryThreshold)
        {
            return false;
        }

        string firstId = baselineEntries[0].Id;
        return baselineEntries.All(entry =>
            string.Equals(entry.Id, firstId, StringComparison.OrdinalIgnoreCase));
    }

    private IArchitectureContract? FindFirst(string contractId)
    {
        foreach (IArchitectureContract candidate in _contracts)
        {
            if (_workCounter is not null)
            {
                _workCounter.ContractLinearScanComparisons++;
            }

            if (string.Equals(candidate.Id, contractId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private Dictionary<string, IArchitectureContract> BuildIndex()
    {
        var contractsById = new Dictionary<string, IArchitectureContract>(StringComparer.OrdinalIgnoreCase);
        foreach (IArchitectureContract candidate in _contracts)
        {
            if (_workCounter is not null)
            {
                _workCounter.ContractIndexEntries++;
            }

            if (candidate.Id is { } contractId)
            {
                contractsById.TryAdd(contractId, candidate);
            }
        }

        return contractsById;
    }
}
