using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal interface IArchitecturePolicyDocumentValidator
{
    void Validate(ArchitectureContractDocument document);
}
