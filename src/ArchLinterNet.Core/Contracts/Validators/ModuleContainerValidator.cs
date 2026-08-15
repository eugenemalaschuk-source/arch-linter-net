using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class ModuleContainerValidator : IArchitecturePolicyDocumentValidator
{
    private const string CliCommandProfile = "cli_command";

    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureModuleContainerContract contract in document.Provenance.Track(
                     document.Contracts.StrictModuleContainers.Concat(document.Contracts.AuditModuleContainers)))
        {
            if (string.IsNullOrWhiteSpace(contract.Container) || contract.Container.EndsWith(".", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Module container contract '{contract.Name}' requires a non-empty dot-separated container namespace.");
            }

            if (!string.Equals(contract.Profile, CliCommandProfile, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Module container contract '{contract.Name}' declares unsupported profile '{contract.Profile}'. Supported profiles: {CliCommandProfile}.");
            }

            ValidateTypeNames(contract, contract.AllowedContainerRootTypes, "allowed_container_root_types");
            ValidateTypeNames(contract, contract.AllowedModuleRootTypes, "allowed_module_root_types");
        }
    }

    private static void ValidateTypeNames(
        ArchitectureModuleContainerContract contract,
        IReadOnlyList<string> typeNames,
        string field)
    {
        for (int index = 0; index < typeNames.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(typeNames[index]))
            {
                throw new InvalidOperationException(
                    $"Module container contract '{contract.Name}' has a blank {field} entry at index {index}.");
            }
        }
    }
}
