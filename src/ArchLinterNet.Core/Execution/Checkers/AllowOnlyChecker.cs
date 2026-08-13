using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class AllowOnlyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureAllowOnlyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, contract.Source);
        Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);

        var allowedLayers = contract.Allowed
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerName))
            .Append(sourceLayer)
            .ToList();

        return sourceTypes
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;
                string[] forbiddenRefs = ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(refType => new
                    {
                        FullName = ArchitectureTypeNames.SafeFullName(refType),
                        Namespace = ArchitectureTypeNames.SafeNamespace(refType),
                        Type = refType
                    })
                    .Where(r => !string.IsNullOrEmpty(r.FullName))
                    .Where(r => !contract.AllowedTypes.Contains(r.FullName))
                    .Where(r => r.Type != null && context.IsInAnyDeclaredLayer(r.Type))
                    .Where(r => !ArchitectureNamespaceViolationFinder.IsInAnyAllowedLayer(
                        r.Type, allowedLayers, context.RoleIndex, context.ExpressionFacts))
                    .Where(r => !executionContext.IsIgnored(
                        sourceFullName,
                        r.FullName,
                        sourceAssembly: sourceAssembly,
                        targetAssembly: ArchitectureTypeNames.SafeAssemblyName(r.Type),
                        targetType: r.FullName,
                        targetMember: r.FullName))
                    .Select(r => r.FullName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray();
                return new ArchitectureViolation(
                    contract.Name, contract.Id, sourceFullName, "outside allowed layers", forbiddenRefs);
            })
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToList();
    }
}
