using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class DuplicateIdValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        IEnumerable<IArchitectureContract>[] groups =
        [
            document.Contracts.Strict,
            document.Contracts.Audit,
            document.Contracts.StrictLayers,
            document.Contracts.AuditLayers,
            document.Contracts.StrictAllowOnly,
            document.Contracts.AuditAllowOnly,
            document.Contracts.StrictCycles,
            document.Contracts.AuditCycles,
            document.Contracts.StrictMethodBody,
            document.Contracts.AuditMethodBody,
            document.Contracts.StrictAsmdef,
            document.Contracts.AuditAsmdef,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictAssemblyIndependence,
            document.Contracts.AuditAssemblyIndependence,
            document.Contracts.StrictAssemblyDependency,
            document.Contracts.AuditAssemblyDependency,
            document.Contracts.StrictAssemblyAllowOnly,
            document.Contracts.AuditAssemblyAllowOnly,
            document.Contracts.StrictPackageDependency,
            document.Contracts.AuditPackageDependency,
            document.Contracts.StrictPackageAllowOnly,
            document.Contracts.AuditPackageAllowOnly,
            document.Contracts.StrictProjectMetadata,
            document.Contracts.AuditProjectMetadata,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictExternalAllowOnly,
            document.Contracts.AuditExternalAllowOnly,
            document.Contracts.StrictLayerTemplates,
            document.Contracts.AuditLayerTemplates,
            document.Contracts.StrictAcyclicSiblings,
            document.Contracts.AuditAcyclicSiblings,
            document.Contracts.StrictTypePlacement,
            document.Contracts.AuditTypePlacement,
            document.Contracts.StrictPublicApiSurface,
            document.Contracts.AuditPublicApiSurface,
            document.Contracts.StrictAttributeUsage,
            document.Contracts.AuditAttributeUsage,
            document.Contracts.StrictInheritance,
            document.Contracts.AuditInheritance,
            document.Contracts.StrictInterfaceImplementation,
            document.Contracts.AuditInterfaceImplementation,
            document.Contracts.StrictComposition,
            document.Contracts.AuditComposition,
            document.Contracts.StrictCoverage,
            document.Contracts.AuditCoverage,
        ];

        foreach (var group in groups)
        {
            var duplicates = group
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate contract IDs found: {string.Join(", ", duplicates)}. Each contract ID must be unique within its contract type and mode group.");
            }
        }
    }
}
