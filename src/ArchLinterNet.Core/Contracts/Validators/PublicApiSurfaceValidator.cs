using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class PublicApiSurfaceValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePublicApiSurfaceContract contract in document.Contracts.StrictPublicApiSurface
                     .Concat(document.Contracts.AuditPublicApiSurface))
        {
            if (contract.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Public API surface contract '{contract.Name}' declares no 'assemblies'. " +
                    "A contract with nothing to scan is a configuration error; declare at least one target assembly.");
            }

            string? invalidAssembly = contract.Assemblies.FirstOrDefault(a => !targetAssemblies.Contains(a));
            if (invalidAssembly != null)
            {
                throw new InvalidOperationException(
                    $"Public API surface contract '{contract.Name}' references assembly '{invalidAssembly}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                    "'strict_public_api_surface'/'audit_public_api_surface' must be a declared target assembly, " +
                    "otherwise a typo'd assembly name would silently disable the contract instead of failing loudly.");
            }
        }
    }
}
