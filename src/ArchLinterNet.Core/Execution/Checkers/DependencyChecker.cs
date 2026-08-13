using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class DependencyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureDependencyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, contract.Source);
        Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);

        List<ArchitectureViolation> violations = new();
        bool transitive = contract.DependencyDepth == DependencyDepthMode.Transitive;

        foreach (string forbiddenLayerName in contract.Forbidden)
        {
            ArchitectureLayer forbiddenLayer =
                ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, forbiddenLayerName);
            AddLayerViolations(
                violations, context, executionContext, sourceTypes, forbiddenLayer, contract.AllowedTypes, transitive);
        }

        if (contract.ForbiddenLegacyRuntime)
        {
            foreach (string forbiddenNamespace in context.Document.LegacyRuntimeLayers)
            {
                AddLayerViolations(
                    violations, context, executionContext, sourceTypes,
                    new ArchitectureLayer { Namespace = forbiddenNamespace }, contract.AllowedTypes, transitive);
            }
        }

        return violations;
    }

    private static void AddLayerViolations(
        List<ArchitectureViolation> violations,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        List<string> allowedTypes,
        bool transitive)
    {
        if (transitive)
        {
            violations.AddRange(ArchitectureNamespaceViolationFinder.FindTransitiveNamespaceViolations(
                sourceTypes, forbiddenLayer, allowedTypes, context.AnalysisContext.TargetAssemblies, executionContext,
                context.ReferenceGraph, context.RoleIndex, context.ExpressionFacts));
            return;
        }

        violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
            sourceTypes, forbiddenLayer, allowedTypes, executionContext,
            context.ReferenceGraph, context.RoleIndex, context.ExpressionFacts));
    }
}
