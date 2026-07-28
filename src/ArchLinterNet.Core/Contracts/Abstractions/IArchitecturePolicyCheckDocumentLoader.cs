using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

internal interface IArchitecturePolicyCheckDocumentLoader
{
    ArchitectureContractDocument LoadForPolicyCheck(string policyPath);
}
