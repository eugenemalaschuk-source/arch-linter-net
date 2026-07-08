using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class PackageAllowOnlyValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePackageAllowOnlyContract contract in document.Contracts.StrictPackageAllowOnly
                     .Concat(document.Contracts.AuditPackageAllowOnly))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Package allow-only contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_package_allow_only'/'audit_package_allow_only' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive package-reference " +
                    "resolution is not supported.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Package allow-only contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_package_allow_only'/'audit_package_allow_only' contract must be a declared target assembly.");
            }
        }
    }
}
