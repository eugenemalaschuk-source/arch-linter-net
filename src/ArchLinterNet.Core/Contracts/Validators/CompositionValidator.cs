using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class CompositionValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureCompositionContract contract in document.Contracts.StrictComposition
                     .Concat(document.Contracts.AuditComposition))
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
                || PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.AllowedOnlyInAssemblies);

            if (!hasAllowedOnlyExpectation)
            {
                throw new InvalidOperationException(
                    $"Composition contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "composition boundary. Declare at least one, or every call site in the codebase would be " +
                    "considered outside the boundary.");
            }
        }
    }
}
