using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli;

internal sealed class CliRuntime : ICliRuntime
{
    private readonly ArchitectureDiagnosticFormatter _formatter = new();
    private readonly ArchitectureSarifFormatter _sarifFormatter = new();
    private readonly ArchitectureEngine _engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

    public string Version { get; } = typeof(ArchitectureEngine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
    {
        switch (value)
        {
            case "namespace":
                level = ArchitectureGraphLevel.Namespace;
                return true;
            case "type":
                level = ArchitectureGraphLevel.Type;
                return true;
            case "assembly":
                level = ArchitectureGraphLevel.Assembly;
                return true;
            default:
                level = default;
                return false;
        }
    }

    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
    {
        return _engine.Validate(request, timing);
    }

    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation> coverageFindings,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
        IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries)
    {
        return _formatter.FormatResultForCiArtifacts(
            mode,
            passed,
            violations,
            cycles,
            coverageFindings,
            unmatchedIgnoredViolations,
            policyConsistencyFindings,
            coverageSummaries);
    }

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles)
    {
        return _sarifFormatter.FormatResultAsSarif(mode, violations, cycles, Version);
    }

    public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return _formatter.FormatViolationsForHumans(violations);
    }

    public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        return _formatter.FormatCyclesForHumans(cycles);
    }

    public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics)
    {
        return _formatter.FormatPolicyConsistencyForHumans(diagnostics);
    }

    public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations)
    {
        return _formatter.FormatUnmatchedForHumans(unmatchedViolations);
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings)
    {
        return _formatter.FormatCoverageForHumans(coverageFindings);
    }

    public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries)
    {
        return _formatter.FormatCoverageSummaryForHumans(coverageSummaries);
    }

    public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request)
    {
        return _engine.GenerateBaseline(request);
    }

    public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request)
    {
        return _engine.UpdateBaseline(request);
    }

    public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request)
    {
        return _engine.PruneBaseline(request);
    }

    public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
    {
        return _engine.DiffBaseline(request);
    }

    public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request)
    {
        return _engine.VerifyBaseline(request);
    }

    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        return _engine.BuildGraph(request);
    }

    public string FormatGraphAsJson(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsJson(graph);
    }

    public string FormatGraphAsDot(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsDot(graph);
    }

    public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsMermaid(graph);
    }

    public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request)
    {
        return _engine.Explain(request);
    }
}
