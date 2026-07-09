using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Abstractions;

internal interface ICliRuntime
{
    string Version { get; }

    bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level);

    ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing);

    string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation> coverageFindings,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
        IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries);

    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles);

    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    string FormatCyclesForHumans(IReadOnlyCollection<string> cycles);

    string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics);

    string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations);

    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings);

    string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries);

    BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request);

    BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request);

    BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request);

    BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request);

    BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request);

    ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request);

    string FormatGraphAsJson(ArchitectureDependencyGraph graph);

    string FormatGraphAsDot(ArchitectureDependencyGraph graph);

    string FormatGraphAsMermaid(ArchitectureDependencyGraph graph);

    ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request);
}
