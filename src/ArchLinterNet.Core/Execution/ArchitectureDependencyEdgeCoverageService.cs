using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureDependencyEdgeCoverageService
{
    private readonly ArchitectureAnalysisSession _session;
    private readonly ArchitectureCoverageAnalysisService _coverage;

    public ArchitectureDependencyEdgeCoverageService(
        ArchitectureAnalysisSession session,
        ArchitectureCoverageAnalysisService coverage)
    {
        _session = session;
        _coverage = coverage;
    }

    internal List<ArchitectureViolation> Check(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = _session.BuildCoverageInventory(_session.Document);
        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> findings = new();

        foreach (List<string> pair in contract.Between)
        {
            string sourceLayer = pair[0];
            string targetLayer = pair[1];
            if (contract.Exclude.Any(exclusion => ArchitectureCoverageAnalysisService.MatchesDependencyEdgeExclusion(exclusion, sourceLayer, targetLayer))
                || _coverage.IsLayerPairGoverned(sourceLayer, targetLayer))
            {
                continue;
            }

            foreach (ArchitectureCoverageDependencyEdge edge in _coverage.GetEdgesForLayerPair(sourceLayer, targetLayer))
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
                    new[] { ArchitectureCoverageAnalysisService.GetRepresentativeNamespaceType(inventory, edge.SourceNamespace) }));
            }
        }

        _session.CollectUnmatchedIgnores(executionContext);
        return findings.OrderBy(f => f.SourceType, StringComparer.Ordinal).ToList();
    }
}
