using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class ProtectedChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureProtectedContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();
        HashSet<string> allowedTypes = new(contract.AllowedTypes, StringComparer.Ordinal);

        HashSet<string> allLayerNames = new(context.Document.Layers.Keys, StringComparer.Ordinal);

        List<ArchitectureLayer> allowedImporterLayers = contract.AllowedImporters
            .Select(name => ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, name))
            .ToList();

        foreach (string protectedLayerName in contract.Protected)
        {
            ArchitectureLayer protectedLayer =
                ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, protectedLayerName);

            var scope = new ProtectedScope(
                protectedLayerName, protectedLayer, allowedImporterLayers, allowedTypes, allLayerNames);

            CollectProtectedLayerViolations(contract, context, scope, executionContext, violations);
        }

        return violations;
    }

    // The resolved inputs for one `protected:` entry of the contract, bundled so the per-type
    // methods below take one scope instead of five separately-threaded resolution results.
    private sealed record ProtectedScope(
        string ProtectedLayerName,
        ArchitectureLayer ProtectedLayer,
        List<ArchitectureLayer> AllowedImporterLayers,
        HashSet<string> AllowedTypes,
        HashSet<string> AllLayerNames);

    private static void CollectProtectedLayerViolations(
        ArchitectureProtectedContract contract,
        ArchitectureCheckerContext context,
        ProtectedScope scope,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach (Assembly assembly in context.AnalysisContext.TargetAssemblies)
        {
            foreach (Type sourceType in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                ArchitectureViolation? violation = BuildViolation(
                    contract, context, sourceType, scope, executionContext);

                if (violation != null)
                {
                    violations.Add(violation);
                }
            }
        }
    }

    private static ArchitectureViolation? BuildViolation(
        ArchitectureProtectedContract contract,
        ArchitectureCheckerContext context,
        Type sourceType,
        ProtectedScope scope,
        ArchitectureContractExecutionContext executionContext)
    {
        string sourceTypeFullName = ArchitectureTypeNames.SafeFullName(sourceType);
        if (string.IsNullOrEmpty(sourceTypeFullName))
        {
            return null;
        }

        if (context.MatchesLayer(scope.ProtectedLayer, sourceType))
        {
            return null;
        }

        if (scope.AllowedImporterLayers.Any(layer => context.MatchesLayer(layer, sourceType)))
        {
            return null;
        }

        string? sourceLayerName = context.ResolveContainingLayer(sourceType, scope.AllLayerNames);

        List<string> matchingRefs = new();
        HashSet<string> matchedNamespacePrefixes = new(StringComparer.Ordinal);

        CollectProtectedLayerReferences(
            context, sourceType, sourceTypeFullName, scope, executionContext, matchingRefs, matchedNamespacePrefixes);

        if (matchingRefs.Count == 0)
        {
            return null;
        }

        string[] normalizedRefs = matchingRefs
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return new ArchitectureViolation(
            contract.Name, contract.Id,
            sourceTypeFullName,
            $"protected layer '{scope.ProtectedLayerName}' (allowed importers: [{string.Join(", ", contract.AllowedImporters)}])",
            normalizedRefs)
        {
            MatchedNamespacePrefixes = matchedNamespacePrefixes.Count > 0
                ? matchedNamespacePrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray()
                : null,
            Payload = new DependencyPayload(
                SourceLayer: sourceLayerName,
                TargetLayer: scope.ProtectedLayerName,
                AllowedImporters: contract.AllowedImporters)
        };
    }

    private static void CollectProtectedLayerReferences(
        ArchitectureCheckerContext context,
        Type sourceType,
        string sourceTypeFullName,
        ProtectedScope scope,
        ArchitectureContractExecutionContext executionContext,
        List<string> matchingRefs,
        HashSet<string> matchedNamespacePrefixes)
    {
        foreach (Type refType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
        {
            string refFullName = ArchitectureTypeNames.SafeFullName(refType);
            if (string.IsNullOrEmpty(refFullName))
            {
                continue;
            }

            if (!context.MatchesLayer(scope.ProtectedLayer, refType))
            {
                continue;
            }

            if (scope.AllowedTypes.Contains(sourceTypeFullName))
            {
                continue;
            }

            if (executionContext.IsIgnored(
                    sourceTypeFullName,
                    refFullName,
                    sourceAssembly: ArchitectureTypeNames.SafeAssemblyName(sourceType),
                    targetAssembly: ArchitectureTypeNames.SafeAssemblyName(refType),
                    targetType: refFullName,
                    targetMember: refFullName))
            {
                continue;
            }

            matchingRefs.Add(refFullName);
            ArchitectureNamespaceMatch protectedMatch = ArchitectureLayerResolver.MatchNamespace(
                scope.ProtectedLayer, ArchitectureTypeNames.SafeNamespace(refType));
            if (!string.IsNullOrEmpty(protectedMatch.MatchedNamespacePrefix))
            {
                matchedNamespacePrefixes.Add(protectedMatch.MatchedNamespacePrefix);
            }
        }
    }
}
