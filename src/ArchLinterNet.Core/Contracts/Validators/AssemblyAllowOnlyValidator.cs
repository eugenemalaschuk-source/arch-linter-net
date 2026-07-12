using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class AssemblyAllowOnlyValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureAssemblyAllowOnlyContract contract in document.Contracts.StrictAssemblyAllowOnly
                     .Concat(document.Contracts.AuditAssemblyAllowOnly))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Assembly allow-only contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_assembly_allow_only'/'audit_assembly_allow_only' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive assembly-reference-path " +
                    "resolution is a planned follow-up.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Assembly allow-only contract '{contract.Name}' references source assembly '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                    "'strict_assembly_allow_only'/'audit_assembly_allow_only' must be a declared target assembly.");
            }

            string? invalidAssembly = contract.Allowed.FirstOrDefault(a => !targetAssemblies.Contains(a));
            if (invalidAssembly != null)
            {
                throw new InvalidOperationException(
                    $"Assembly allow-only contract '{contract.Name}' references allowed assembly '{invalidAssembly}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                    "'strict_assembly_allow_only'/'audit_assembly_allow_only' must be a declared target assembly.");
            }
        }
    }
}
