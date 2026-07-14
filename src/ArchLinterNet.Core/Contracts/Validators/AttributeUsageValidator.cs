using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class AttributeUsageValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureAttributeUsageContract contract in document.Provenance.Track(
                     document.Contracts.StrictAttributeUsage.Concat(document.Contracts.AuditAttributeUsage)))
        {
            if (contract.Attributes.Count == 0 && contract.AttributePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Attribute usage contract '{contract.Name}' declares no 'attributes' or 'attribute_prefixes'. " +
                    "A contract with nothing to match against is a configuration error; declare at least one " +
                    "fully-qualified attribute type name or attribute type-name/namespace prefix.");
            }

            bool hasAllowedOnlyExpectation = contract.AllowedOnlyInLayers.Count > 0
                || contract.AllowedOnlyInNamespaces.Count > 0
                || contract.AllowedOnlyInProjects.Count > 0
                || contract.AllowedOnlyInAssemblies.Count > 0;

            bool hasForbiddenExpectation = contract.ForbiddenInLayers.Count > 0
                || contract.ForbiddenInNamespaces.Count > 0
                || contract.ForbiddenInProjects.Count > 0
                || contract.ForbiddenInAssemblies.Count > 0;

            if (!hasAllowedOnlyExpectation && !hasForbiddenExpectation)
            {
                throw new InvalidOperationException(
                    $"Attribute usage contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "or forbidden_in_layers/forbidden_in_namespaces/forbidden_in_projects/forbidden_in_assemblies " +
                    "location expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }
}
