using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureContractSelectionService
{
    private readonly ArchitectureAnalysisSession _session;

    public ArchitectureContractSelectionService(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    // Source-set expansion derives per-instance ids ("<authored-id>/<source>"), so selecting the
    // authored id a policy author actually wrote must still select every instance it produced.
    // Contracts that were never expanded fall through to the ordinary id-only check.
    public bool IsContractSelected(IArchitectureContract contract)
    {
        return _session.IsContractSelected(contract.Id)
            || (_session.SelectedContractIds is { Count: > 0 }
                && contract is IArchitectureSourceExpandableContract { ExpansionOrigin: { } origin }
                && _session.SelectedContractIds.Contains(origin.AuthoredContractId));
    }

    // Every id a request or a coverage contract may legitimately use to name this contract: its own
    // id and, for an expanded instance, the authored id the policy author actually wrote.
    private static string[] ContractIdAliases(IArchitectureContract contract)
    {
        return new[]
            {
                contract.Id,
                (contract as IArchitectureSourceExpandableContract)?.ExpansionOrigin?.AuthoredContractId
            }
            .OfType<string>()
            .ToArray();
    }

    // A contract whose layer-bearing field names a layer absent from `layers` would otherwise throw
    // via ArchitectureLayerResolver.ResolveLayer the moment its check runs. When a rule_input
    // coverage contract that will actually execute this request already tracks this contract — by
    // its own id or by its authored id — defer entirely to that coverage contract's "unresolved"
    // finding instead of crashing, mirroring CheckConfiguration's "empty layer namespace" deferral.
    public bool IsDanglingButCoveredByRuleInputCoverage(IArchitectureContract contract)
    {
        if (contract.Id == null || _session.RuleInputCoveredContractIdsForMode == null)
        {
            return false;
        }

        if (!ContractIdAliases(contract).Any(_session.RuleInputCoveredContractIdsForMode.Contains))
        {
            return false;
        }

        return ArchitecturePolicyConsistencyAnalysisService.GetReferencedLayerNames(contract)
            .Any(layerName => !_session.Document.Layers.ContainsKey(layerName));
    }
}
