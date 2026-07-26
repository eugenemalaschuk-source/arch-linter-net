using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // Source-set expansion derives per-instance ids ("<authored-id>/<source>"), so selecting the
    // authored id a policy author actually wrote must still select every instance it produced.
    // Contracts that were never expanded fall through to the ordinary id-only check.
    public bool IsContractSelected(IArchitectureContract contract)
    {
        return IsContractSelected(contract.Id)
            || (SelectedContractIds is { Count: > 0 }
                && contract is IArchitectureSourceExpandableContract { ExpansionOrigin: { } origin }
                && SelectedContractIds.Contains(origin.AuthoredContractId));
    }
}
