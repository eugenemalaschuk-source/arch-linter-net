using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class InterfaceImplementationValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureInterfaceImplementationContract contract in document.Contracts.StrictInterfaceImplementation
                     .Concat(document.Contracts.AuditInterfaceImplementation))
        {
            if (contract.Interfaces.Count == 0 && contract.InterfacePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Interface implementation contract '{contract.Name}' declares no 'interfaces' or " +
                    "'interface_prefixes'. A contract with nothing to match against is a configuration error; " +
                    "declare at least one fully-qualified interface name or interface type-name/namespace prefix.");
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
                    $"Interface implementation contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "or forbidden_in_layers/forbidden_in_namespaces/forbidden_in_projects/forbidden_in_assemblies " +
                    "location expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }
}
