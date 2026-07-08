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
            foreach (string assemblyName in contract.Assemblies)
            {
                if (!targetAssemblies.Contains(assemblyName))
                {
                    throw new InvalidOperationException(
                        $"Assembly independence contract '{contract.Name}' references assembly '{assemblyName}' " +
                        "that is not declared in 'analysis.target_assemblies'. Every assembly listed in " +
                        "'strict_assembly_independence'/'audit_assembly_independence' must be a declared target assembly.");
                }
            }
        }
    }
}
