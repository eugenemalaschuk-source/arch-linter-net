using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// The layer-scoped "independence" family. Named for its subject to stay distinguishable from
// AssemblyIndependenceChecker, which governs the assembly-scoped family.
internal static class LayerIndependenceChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureIndependenceContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, sourceLayerName);
            Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);

            foreach (string forbiddenLayerName in contract.Layers)
            {
                if (string.Equals(sourceLayerName, forbiddenLayerName, StringComparison.Ordinal))
                {
                    continue;
                }

                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, forbiddenLayerName);
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                    sourceTypes, forbiddenLayer, Array.Empty<string>(), executionContext, null,
                    context.RoleIndex, context.ExpressionFacts));
            }
        }

        return violations;
    }
}
