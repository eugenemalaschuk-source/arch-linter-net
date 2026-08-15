using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

/// <summary>
/// Selects the scope-specific coverage summary projection while leaving evidence collection and
/// contract checking with <see cref="ArchitectureCoverageAnalysisService"/>.
/// </summary>
internal sealed class ArchitectureCoverageSummaryService
{
    private readonly ArchitectureAnalysisSession _session;
    private readonly ArchitectureCoverageAnalysisService _coverage;
    private readonly ArchitectureSemanticCoverageService _semanticCoverage;

    public ArchitectureCoverageSummaryService(
        ArchitectureAnalysisSession session,
        ArchitectureCoverageAnalysisService coverage,
        ArchitectureSemanticCoverageService semanticCoverage)
    {
        _session = session;
        _coverage = coverage;
        _semanticCoverage = semanticCoverage;
    }

    internal ArchitectureCoverageSummary? Build(ArchitectureCoverageContract contract)
    {
        if (!_session.IsContractSelected(contract.Id))
        {
            return null;
        }

        return contract.Scope switch
        {
            "rule_input" => _coverage.BuildRuleInputSummary(contract),
            "assembly" => _coverage.BuildAssemblySummary(contract),
            "project" => _coverage.BuildProjectSummary(contract),
            "dependency_edge" => _coverage.BuildDependencyEdgeSummary(contract),
            "semantic_role" => _semanticCoverage.BuildSummary(contract),
            _ => _coverage.BuildNamespaceSummary(contract)
        };
    }
}
