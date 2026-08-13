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

            CollectProtectedLayerViolations(contract, context, protectedLayerName, protectedLayer,
                allowedImporterLayers, allowedTypes, allLayerNames, executionContext, violations);
        }

        return violations;
    }

    private static void CollectProtectedLayerViolations( // NOSONAR: contract resolution inputs intentionally stay explicit.
        ArchitectureProtectedContract contract,
        ArchitectureCheckerContext context,
        string protectedLayerName,
        ArchitectureLayer protectedLayer,
        List<ArchitectureLayer> allowedImporterLayers,
        HashSet<string> allowedTypes,
        HashSet<string> allLayerNames,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach (Assembly assembly in context.AnalysisContext.TargetAssemblies)
        {
            foreach (Type sourceType in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                ArchitectureViolation? violation = BuildViolation(
                    contract, context, sourceType, protectedLayer, protectedLayerName, allowedImporterLayers,
                    allowedTypes, allLayerNames, executionContext);

                if (violation != null)
                {
                    violations.Add(violation);
                }
            }
        }
    }

    private static ArchitectureViolation? BuildViolation( // NOSONAR: explicit contract context aids diagnostic construction.
        ArchitectureProtectedContract contract,
        ArchitectureCheckerContext context,
        Type sourceType,
        ArchitectureLayer protectedLayer,
        string protectedLayerName,
        List<ArchitectureLayer> allowedImporterLayers,
        HashSet<string> allowedTypes,
        HashSet<string> allLayerNames,
        ArchitectureContractExecutionContext executionContext)
    {
        string sourceTypeFullName = ArchitectureTypeNames.SafeFullName(sourceType);
        if (string.IsNullOrEmpty(sourceTypeFullName))
        {
            return null;
        }

        if (context.MatchesLayer(protectedLayer, sourceType))
        {
            return null;
        }

        if (allowedImporterLayers.Any(layer => context.MatchesLayer(layer, sourceType)))
        {
            return null;
        }

        string? sourceLayerName = context.ResolveContainingLayer(sourceType, allLayerNames);

        List<string> matchingRefs = new();
        HashSet<string> matchedNamespacePrefixes = new(StringComparer.Ordinal);

        CollectProtectedLayerReferences(context, sourceType, sourceTypeFullName, protectedLayer, allowedTypes,
            executionContext, matchingRefs, matchedNamespacePrefixes);

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
            $"protected layer '{protectedLayerName}' (allowed importers: [{string.Join(", ", contract.AllowedImporters)}])",
            normalizedRefs)
        {
            MatchedNamespacePrefixes = matchedNamespacePrefixes.Count > 0
                ? matchedNamespacePrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray()
                : null,
            Payload = new DependencyPayload(
                SourceLayer: sourceLayerName,
                TargetLayer: protectedLayerName,
                AllowedImporters: contract.AllowedImporters)
        };
    }

    private static void CollectProtectedLayerReferences(
        ArchitectureCheckerContext context,
        Type sourceType,
        string sourceTypeFullName,
        ArchitectureLayer protectedLayer,
        HashSet<string> allowedTypes,
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

            if (!context.MatchesLayer(protectedLayer, refType))
            {
                continue;
            }

            if (allowedTypes.Contains(sourceTypeFullName))
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
                protectedLayer, ArchitectureTypeNames.SafeNamespace(refType));
            if (!string.IsNullOrEmpty(protectedMatch.MatchedNamespacePrefix))
            {
                matchedNamespacePrefixes.Add(protectedMatch.MatchedNamespacePrefix);
            }
        }
    }
}
