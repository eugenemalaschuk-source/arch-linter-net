using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// Shared by the "layer" and "layer_template" families: layer_template contracts are expanded into
// ArchitectureLayerContract instances before execution, so they run through this same checker.
internal static class LayerChecker
{
    // Exhaustive-sibling findings are produced after the execution context has collected its
    // unmatched ignores in the pre-extraction ordering, so they are returned separately and appended
    // by the session wrapper rather than merged into `violations` here.
    internal sealed record Result(
        List<ArchitectureViolation> Violations,
        List<ArchitectureViolation> ExhaustiveSiblingViolations);

    public static Result Check(
        ArchitectureLayerContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        var effectiveLayers = new List<(string name, ArchitectureLayer layer, Type[] types)>();

        foreach (string layerEntry in contract.Layers)
        {
            ArchitectureLayer layer = ResolveLayerEntry(context, contract, layerEntry);
            Type[] types = context.FindTypesInLayer(layer);

            if (types.Length == 0)
            {
                if (contract.OptionalLayers.Contains(layerEntry))
                {
                    continue;
                }

                ArchitectureViolation? emptyLayerViolation = BuildEmptyLayerViolation(contract, layerEntry, layer);
                if (emptyLayerViolation != null)
                {
                    violations.Add(emptyLayerViolation);
                }
            }

            effectiveLayers.Add((layerEntry, layer, types));
        }

        CollectLayerOrderingViolations(effectiveLayers, contract, context, executionContext, violations);

        List<ArchitectureViolation> exhaustiveSiblingViolations = new();
        CollectExhaustiveSiblingViolations(effectiveLayers, contract, context, exhaustiveSiblingViolations);

        return new Result(violations, exhaustiveSiblingViolations);
    }

    private static ArchitectureLayer ResolveLayerEntry(
        ArchitectureCheckerContext context,
        ArchitectureLayerContract contract,
        string layerEntry)
    {
        if (contract.TemplateName != null)
        {
            return new ArchitectureLayer { Namespace = layerEntry };
        }

        return ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, layerEntry);
    }

    private static ArchitectureViolation? BuildEmptyLayerViolation(
        ArchitectureLayerContract contract, string layerEntry, ArchitectureLayer layer)
    {
        if (layer.External || contract.TemplateName == null)
        {
            return null;
        }

        string matchDescription = layer.Selector == null
            ? $"namespace '{layer.Namespace}'"
            : $"semantic selector '{ArchitectureLayerResolver.DescribeLayer(layer)}'";

        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            ArchitectureLayerResolver.DescribeLayer(layer),
            layer.Selector == null ? "empty layer namespace" : "empty layer selector",
            new[] { $"Required layer '{layerEntry}' {matchDescription} contains no matching types in loaded assemblies." })
        {
            Payload = new ConfigurationPayload(
                TemplateName: contract.TemplateName,
                ContainerNamespace: contract.ContainerNamespace)
        };
    }

    private static void CollectLayerOrderingViolations(
        List<(string name, ArchitectureLayer layer, Type[] types)> effectiveLayers,
        ArchitectureLayerContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        for (int sourceIndex = 0; sourceIndex < effectiveLayers.Count; sourceIndex++)
        {
            var (_, _, sourceTypes) = effectiveLayers[sourceIndex];

            for (int forbiddenIndex = 0; forbiddenIndex < sourceIndex; forbiddenIndex++)
            {
                var (_, forbiddenLayer, _) = effectiveLayers[forbiddenIndex];
                foreach (ArchitectureViolation v in ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                    sourceTypes, forbiddenLayer, Array.Empty<string>(), executionContext, context.ReferenceGraph,
                    context.RoleIndex, context.ExpressionFacts))
                {
                    violations.Add(v with
                    {
                        Payload = new ConfigurationPayload(
                            TemplateName: contract.TemplateName,
                            ContainerNamespace: contract.ContainerNamespace)
                    });
                }
            }
        }
    }

    private static void CollectExhaustiveSiblingViolations(
        List<(string name, ArchitectureLayer layer, Type[] types)> effectiveLayers,
        ArchitectureLayerContract contract,
        ArchitectureCheckerContext context,
        List<ArchitectureViolation> violations)
    {
        if (!contract.Exhaustive || contract.ContainerNamespace == null)
        {
            return;
        }

        HashSet<string> expectedNamespaces = new(
            effectiveLayers.Select(l => l.layer.Namespace),
            StringComparer.Ordinal);

        foreach (string childNs in context.TypeIndex.FindDirectChildNamespaces(contract.ContainerNamespace)
                     .OrderBy(ns => ns, StringComparer.Ordinal))
        {
            if (expectedNamespaces.Contains(childNs))
            {
                continue;
            }

            Type[] childTypes = context.TypeIndex.FindTypesInNamespace(childNs);

            if (childTypes.Length > 0)
            {
                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    contract.ContainerNamespace,
                    "unmapped sibling namespace",
                    new[] { $"Namespace '{childNs}' contains types but is not mapped into any declared layer in template '{contract.TemplateName}'." })
                {
                    Payload = new ConfigurationPayload(
                        TemplateName: contract.TemplateName,
                        ContainerNamespace: contract.ContainerNamespace)
                });
            }
        }
    }
}
