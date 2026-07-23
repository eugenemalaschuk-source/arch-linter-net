using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class CompositionValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureCompositionContract contract in document.Provenance.Track(
                     document.Contracts.StrictComposition.Concat(document.Contracts.AuditComposition)))
        {
            if (!PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.ForbiddenApis))
            {
                throw new InvalidOperationException(
                    $"Composition contract '{contract.Name}' declares no 'forbidden_apis'. A contract with " +
                    "nothing to match against is a configuration error; declare at least one forbidden API " +
                    "selector (member name, Type.Member name, fully qualified member, or namespace/type prefix).");
            }

            bool hasAllowedOnlyExpectation = PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.AllowedOnlyInLayers)
                || PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.AllowedOnlyInNamespaces)
                || PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.AllowedOnlyInProjects)
                || PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.AllowedOnlyInAssemblies)
                || contract.AllowedOnlyInTypes.Count > 0;

            if (!hasAllowedOnlyExpectation)
            {
                throw new InvalidOperationException(
                    $"Composition contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies/allowed_only_in_types " +
                    "composition boundary. Declare at least one, or every call site in the codebase would be " +
                    "considered outside the boundary.");
            }

            foreach (ArchitectureCompositionTypeSelector typeSelector in contract.AllowedOnlyInTypes)
            {
                if (string.IsNullOrWhiteSpace(typeSelector.Assembly) || string.IsNullOrWhiteSpace(typeSelector.Type))
                {
                    throw new InvalidOperationException(
                        $"Composition contract '{contract.Name}' declares an 'allowed_only_in_types' entry " +
                        "missing 'assembly' or 'type'. Both are required — a type selector without an assembly " +
                        "or type identity cannot match anything.");
                }
            }
        }
    }
}
