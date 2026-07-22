using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class FrameworkReferenceValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureFrameworkReferenceContract contract in document.Provenance.Track(
                     document.Contracts.StrictFrameworkDependency.Concat(document.Contracts.AuditFrameworkDependency)))
        {
            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Framework dependency contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_framework_dependency'/'audit_framework_dependency' contract must be a declared target assembly.");
            }
        }
    }
}
