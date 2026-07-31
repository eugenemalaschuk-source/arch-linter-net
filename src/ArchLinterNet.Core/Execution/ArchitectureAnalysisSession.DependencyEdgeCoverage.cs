using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private List<ArchitectureViolation> CheckDependencyEdgeCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> findings = new();

        foreach (List<string> pair in contract.Between)
        {
            string sourceLayer = pair[0];
            string targetLayer = pair[1];
            if (contract.Exclude.Any(exclusion => MatchesDependencyEdgeExclusion(exclusion, sourceLayer, targetLayer))
                || IsLayerPairGoverned(sourceLayer, targetLayer))
            {
                continue;
            }

            foreach (ArchitectureCoverageDependencyEdge edge in GetEdgesForLayerPair(sourceLayer, targetLayer))
            {
                string edgeKey = $"{edge.SourceNamespace} -> {edge.TargetNamespace}";
                if (executionContext.IsIgnored(
                        edgeKey,
                        "uncovered dependency edge",
                        targetType: edge.TargetNamespace,
                        targetMember: "uncovered dependency edge"))
                {
                    continue;
                }

                findings.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    edgeKey,
                    "uncovered dependency edge",
                    new[] { GetRepresentativeNamespaceType(inventory, edge.SourceNamespace) }));
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return findings.OrderBy(f => f.SourceType, StringComparer.Ordinal).ToList();
    }
}
