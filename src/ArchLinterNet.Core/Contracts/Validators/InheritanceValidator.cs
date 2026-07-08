using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class InheritanceValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureInheritanceContract contract in document.Contracts.StrictInheritance
                     .Concat(document.Contracts.AuditInheritance))
        {
            if (contract.SourceLayers.Count == 0 && contract.SourceNamespaces.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Inheritance contract '{contract.Name}' declares no 'source_layers' or 'source_namespaces'. " +
                    "An empty source surface would silently check no types; declare at least one source layer " +
                    "or namespace prefix.");
            }

            if (contract.ForbiddenBaseTypes.Count == 0 && contract.ForbiddenBaseTypePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Inheritance contract '{contract.Name}' declares no 'forbidden_base_types' or " +
                    "'forbidden_base_type_prefixes'. A contract with nothing to match against is a configuration " +
                    "error; declare at least one fully-qualified base type name or base type-name/namespace prefix.");
            }
        }
    }
}
