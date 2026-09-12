using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

internal abstract class ExplainCommandHandlerTestBase
{
    protected static readonly string[] _value = { "A", "B" };
    protected static ArchitecturePolicyLoadException PolicyException()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        return new ArchitecturePolicyLoadException(
            "Root policy file not found: architecture/root.yml",
            new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.ImportResolution,
                new ArchitecturePolicySourceLocation(source, "$", 1, 1, null, null),
                [],
                source.ImportChain),
            ArchitecturePolicyImportErrorCategory.MissingFile.ToString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    protected static ExplainCommandHandler Handler(
        ExplainStubRuntime runtime,
        RecordingCliConsole console) =>
        new(runtime, console);

    protected static ExplainCommandOptions Options(
        string? source = "A",
        string? target = "B",
        string mode = "strict",
        string level = "namespace",
        string format = "human",
        string? conditionSet = null,
        bool showHelp = false) =>
        new("policy.yml", mode, level, format, conditionSet, source, target, showHelp);

    // ── Guard cases ───────────────────────────────────────────────────────────

    protected sealed class ExplainStubRuntime : ICliRuntime
    {
        private static readonly ArchitectureDependencyGraph _emptyGraph =
            new(Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>());

        public ArchitectureExplainOutcome Outcome { get; init; } =
            new("Source", "Target", null, Array.Empty<string>());

        public Exception? ThrowException { get; init; }

        public ArchitectureExplainRequest? LastRequest { get; private set; }

        public string Version => "1.0.0";

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) =>
            Enum.TryParse(value, true, out level);

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request)
        {
            LastRequest = request;
            return ThrowException == null ? Outcome : throw ThrowException;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => new(_emptyGraph);
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => "digraph G {}";
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => "graph TD";
        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();
        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => throw new NotSupportedException();
        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => throw new NotSupportedException();
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => throw new NotSupportedException();
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => throw new NotSupportedException();
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => throw new NotSupportedException();
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => throw new NotSupportedException();
        public string FormatClassificationFactsForHumans(IReadOnlyCollection<ArchitectureClassificationConflict> conflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();
    }

    protected sealed class RecordingCliConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string OutputText => _output.ToString();
        public string ErrorText => _error.ToString();
    }

}
