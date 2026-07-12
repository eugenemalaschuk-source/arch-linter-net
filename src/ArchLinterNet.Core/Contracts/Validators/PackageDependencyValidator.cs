using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class PackageDependencyValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePackageDependencyContract contract in document.Contracts.StrictPackageDependency
                     .Concat(document.Contracts.AuditPackageDependency))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Package dependency contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_package_dependency'/'audit_package_dependency' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive package-reference " +
                    "resolution is not supported.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Package dependency contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_package_dependency'/'audit_package_dependency' contract must be a declared target assembly.");
            }
        }
    }
}
