using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class AssemblyIndependenceValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureAssemblyIndependenceContract contract in document.Contracts.StrictAssemblyIndependence
                     .Concat(document.Contracts.AuditAssemblyIndependence))
        {
            string? invalidAssembly = contract.Assemblies.FirstOrDefault(a => !targetAssemblies.Contains(a));
            if (invalidAssembly != null)
            {
                throw new InvalidOperationException(
                    $"Assembly independence contract '{contract.Name}' references assembly '{invalidAssembly}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly listed in " +
                    "'strict_assembly_independence'/'audit_assembly_independence' must be a declared target assembly.");
            }
        }
    }
}
