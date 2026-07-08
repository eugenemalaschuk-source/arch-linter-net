using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class AcyclicSiblingValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureAcyclicSiblingContract contract in document.Contracts.StrictAcyclicSiblings
                     .Concat(document.Contracts.AuditAcyclicSiblings))
        {
            if (contract.Ancestors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Acyclic sibling contract '{contract.Name}' has an empty ancestors list. At least one ancestor namespace is required.");
            }

            for (int i = 0; i < contract.Ancestors.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(contract.Ancestors[i]))
                {
                    throw new InvalidOperationException(
                        $"Acyclic sibling contract '{contract.Name}' has a blank or empty ancestor at index {i}. Each ancestor must be a non-empty namespace prefix.");
                }
            }
        }
    }
}
