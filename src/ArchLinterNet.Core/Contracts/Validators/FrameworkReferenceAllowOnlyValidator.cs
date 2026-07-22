using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class FrameworkReferenceAllowOnlyValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureFrameworkReferenceAllowOnlyContract contract in document.Provenance.Track(
                     document.Contracts.StrictFrameworkAllowOnly.Concat(document.Contracts.AuditFrameworkAllowOnly)))
        {
            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Framework allow-only contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_framework_allow_only'/'audit_framework_allow_only' contract must be a declared target assembly.");
            }
        }
    }
}
