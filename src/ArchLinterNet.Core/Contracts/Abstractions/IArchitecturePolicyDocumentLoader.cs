using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitecturePolicyDocumentLoader
{
    ArchitectureContractDocument Load(string policyPath);
}
