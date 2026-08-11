namespace ArchLinterNet.Core.Contracts;

// Post-deserialization preparation stage: contracts that did not declare an explicit `id` get one
// derived from their name, before any validator (notably DuplicateIdValidator) or downstream
// consumer observes the document.
internal static class ArchitectureContractFallbackIds
{
    public static void Assign(ArchitectureContractDocument document)
    {
        foreach (IArchitectureContract contract in GetAllContracts(document).Where(c => string.IsNullOrEmpty(c.Id)))
        {
            contract.Id = ArchitecturePolicyDocumentLoader.NormalizeToContractId(contract.Name);
        }
    }

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }
}
