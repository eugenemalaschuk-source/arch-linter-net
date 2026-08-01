using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitecturePolicyDocumentLoader : IArchitecturePolicyCheckDocumentLoader
{
    ArchitectureContractDocument IArchitecturePolicyCheckDocumentLoader.LoadForPolicyCheck(string policyPath)
    {
        return LoadCore(policyPath, validateEffectiveSchema: true, CancellationToken.None);
    }
}
