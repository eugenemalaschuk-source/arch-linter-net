using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class ProjectMetadataValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureProjectMetadataContract contract in document.Provenance.Track(
                     document.Contracts.StrictProjectMetadata.Concat(document.Contracts.AuditProjectMetadata)))
        {
            if (contract.Projects.Count == 0 || contract.Projects.All(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Project metadata contract '{contract.Name}' declares no usable 'projects'. " +
                    "Declare at least one discovered project path, or the contract will never match anything.");
            }

            bool hasExpectation = contract.RequiredProperties.Count > 0
                || contract.ForbiddenProperties.Count > 0
                || contract.AllowedFriendAssemblies is not null
                || PolicyDocumentValidatorSupport.HasNonBlankEntry(contract.ForbiddenProjectReferences);

            if (!hasExpectation)
            {
                throw new InvalidOperationException(
                    $"Project metadata contract '{contract.Name}' declares no metadata expectation. " +
                    "Declare required_properties, forbidden_properties, allowed_friend_assemblies, or " +
                    "forbidden_project_references.");
            }
        }
    }
}
