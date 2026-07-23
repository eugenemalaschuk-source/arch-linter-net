using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class FrameworkReferenceAllowOnlyValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        ArchitectureFrameworkReferenceAllowOnlyContract? invalid = document.Provenance.Track(
                document.Contracts.StrictFrameworkAllowOnly.Concat(document.Contracts.AuditFrameworkAllowOnly))
            .FirstOrDefault(contract => !targetAssemblies.Contains(contract.Source));

        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Framework allow-only contract '{invalid.Name}' references source '{invalid.Source}' " +
                "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                "'strict_framework_allow_only'/'audit_framework_allow_only' contract must be a declared target assembly.");
        }
    }
}
