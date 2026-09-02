using System.CommandLine;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CliHandlerCoverageTests
{
    private static readonly string[] _ruleA = ["rule-a"];
    private static readonly string[] _explainPath = ["Source", "Mid", "Target"];
    private static readonly string[] _helpArgs = ["--help"];
    private static readonly string[] _versionArgs = ["--version"];
    private static readonly string[] _graphUnknownOptionArgs = ["graph", "--unknown"];

    private static ArchitecturePolicyLoadException PolicyException()
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

    [TestCase("invalid", "namespace", "json", "Invalid mode")]
    [TestCase("strict", "invalid", "json", "Invalid level")]
    [TestCase("strict", "namespace", "invalid", "Invalid format")]
    public void Graph_InvalidOptions_ReportError(string mode, string level, string format, string expectedError)
    {
        var console = new RecordingConsole();
        int result = new GraphCommandHandler(new RecordingRuntime(), console).Execute(
            new GraphCommandOptions("policy.yml", mode, level, format, null, Array.Empty<string>(), false));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        if (format == "json")
        {
            Assert.That(console.ErrorText, Is.Empty);
            using JsonDocument document = JsonDocument.Parse(console.OutputText);
            Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain(expectedError));
            return;
        }

        Assert.That(console.OutputText, Is.Empty);
        Assert.That(console.ErrorText, Does.Contain(expectedError));
    }

    [Test]
    public void Graph_RuntimeFailureWithJson_WritesStructuredError()
    {
        var console = new RecordingConsole();
        int result = new GraphCommandHandler(new RecordingRuntime { GraphException = new InvalidOperationException("graph boom") }, console)
            .Execute(new GraphCommandOptions("policy.yml", "strict", "namespace", "json", null, Array.Empty<string>(), false));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Is.Empty);
        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain("graph boom"));
    }

    [Test]
    public void Graph_ValidDotRequest_FormatsGraphAndPreservesOptions()
    {
        var runtime = new RecordingRuntime { GraphText = "digraph G {}" };
        var console = new RecordingConsole();
        int result = new GraphCommandHandler(runtime, console).Execute(
            new GraphCommandOptions("policy.yml", "audit", "type", "dot", "ci", _ruleA, false));

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(runtime.GraphRequest!.Mode, Is.EqualTo("audit"));
        Assert.That(runtime.GraphRequest.Level, Is.EqualTo(ArchitectureGraphLevel.Type));
        Assert.That(runtime.GraphRequest.ContractIds, Is.EqualTo(_ruleA));
        Assert.That(console.OutputText, Does.Contain("digraph G"));
    }

    [Test]
    public void Graph_TypedPolicyFailure_BypassesFileExistsAndWritesJson()
    {
        var runtime = new RecordingRuntime { GraphException = PolicyException() };
        var console = new RecordingConsole();
        int result = new GraphCommandHandler(runtime, console).Execute(
            new GraphCommandOptions("policy.yml", "strict", "namespace", "json", null, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Does.Contain("architecture_policy_error").And.Contain("architecture/root.yml"));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void Explain_HumanPathAndJsonOutput_UseRuntimeOutcome()
    {
        var runtime = new RecordingRuntime
        {
            ExplainResult = new ArchitectureExplainOutcome("Source", "Target", _explainPath, _ruleA)
        };
        var humanConsole = new RecordingConsole();
        var handler = new ExplainCommandHandler(runtime, humanConsole);

        Assert.That(handler.Execute(new ExplainCommandOptions("policy.yml", "strict", "namespace", "human", null, "Source", "Target", false)),
            Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("Source -> Mid -> Target").And.Contain("Contract IDs: rule-a"));

        var jsonConsole = new RecordingConsole();
        Assert.That(new ExplainCommandHandler(runtime, jsonConsole).Execute(
            new ExplainCommandOptions("policy.yml", "strict", "namespace", "json", null, "Source", "Target", false)),
            Is.EqualTo(CliExitCodes.Success));
        Assert.That(jsonConsole.OutputText, Does.Contain("\"source\":\"Source\"").And.Contain("\"rule-a\""));
    }

    [Test]
    public void Explain_MissingArgumentsAndRuntimeFailure_ReportError()
    {
        var missingConsole = new RecordingConsole();
        int missingResult = new ExplainCommandHandler(new RecordingRuntime(), missingConsole).Execute(
            new ExplainCommandOptions("policy.yml", "strict", "namespace", "human", null, null, "Target", false));
        Assert.That(missingResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(missingConsole.ErrorText, Does.Contain("--source and --target are required"));

        var failureConsole = new RecordingConsole();
        var failingRuntime = new RecordingRuntime { ExplainException = new InvalidOperationException("boom") };
        int failureResult = new ExplainCommandHandler(failingRuntime, failureConsole).Execute(
            new ExplainCommandOptions("policy.yml", "strict", "namespace", "human", null, "Source", "Target", false));
        Assert.That(failureResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(failureConsole.ErrorText, Does.Contain("Explain error: boom"));
    }

    [Test]
    public void Host_LegacyHelp_RendersAfterSuccessfulParsing()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_helpArgs);

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.OutputText, Does.Contain("arch-linter-net — architecture contract linter"));
    }

    [Test]
    public void Host_UnknownCommand_FailsClosedWithHelpGuidance()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime())
            .Run(["debt", "--help"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Unknown command or argument: debt"));
            Assert.That(console.ErrorText, Does.Contain("--help"));
            Assert.That(console.OutputText, Is.Empty);
        });
    }

    [Test]
    public void Host_UnknownNestedCommand_FailsClosedWithHelpGuidance()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime())
            .Run(["graph", "debt", "--help"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Unknown command or argument: debt"));
            Assert.That(console.ErrorText, Does.Contain("graph --help"));
            Assert.That(console.OutputText, Is.Empty);
        });
    }

    [Test]
    public void Host_LegacyVersion_RendersAfterSuccessfulParsing()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_versionArgs);

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.OutputText, Does.Contain("arch-linter-net 1.0.0"));
    }

    [TestCase("--help", "debt", "Unknown command or argument: debt")]
    [TestCase("-h", "debt", "Unknown command or argument: debt")]
    [TestCase("--version", "debt", "Unknown command or argument: debt")]
    [TestCase("-v", "debt", "Unknown command or argument: debt")]
    [TestCase("--help", "--bogus-flag", "Unknown option: --bogus-flag")]
    public void Host_LegacyHelpOrVersionWithInvalidInput_FailsClosed(
        string legacyOption,
        string invalidInput,
        string expectedDiagnostic)
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime())
            .Run([legacyOption, invalidInput]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Is.Empty);
            Assert.That(console.ErrorText, Does.Contain(expectedDiagnostic));
            Assert.That(console.ErrorText, Does.Contain("--help"));
        });
    }

    [Test]
    public void Host_LeadingHelpAfterValidFlagWithInvalidInput_FailsClosed()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime())
            .Run(["--strict", "--help", "debt"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Is.Empty);
            Assert.That(console.ErrorText, Does.Contain("Unknown command or argument: debt"));
            Assert.That(console.ErrorText, Does.Contain("--help"));
        });
    }

    [Test]
    public void Host_ParseErrors_AreNormalizedAndIncludeCommandHint()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_graphUnknownOptionArgs);

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain("Unknown option: --unknown"));
        Assert.That(console.ErrorText, Does.Contain("graph --help"));
    }

    [TestCase("capture")]
    [TestCase("diff")]
    [TestCase("verify")]
    public void Host_TopologyNestedParseErrors_UseTheFullCommandPath(string subcommand)
    {
        var console = new RecordingConsole();

        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime())
            .Run(["topology", subcommand, "--unknown"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain($"topology {subcommand} --help"));
            Assert.That(console.ErrorText, Does.Not.Contain($"baseline {subcommand} --help"));
            Assert.That(console.ErrorText, Does.Not.Contain("public-api capture --help"));
        });
    }

    // The legacy validate renderer only emits root help/version after a successful parse.
    // These cases exercise the value-skipping (--policy <value>), flag (--strict), unknown-token,
    // and dangling-option-value branches that leave TryHandleLegacyValidateShortCircuit without rendering.
    private static readonly string[] _policyThenFlagArgs = ["--policy", "custom.yml", "--strict"];
    private static readonly string[] _unknownLeadingTokenArgs = ["not-an-option"];
    private static readonly string[] _danglingPolicyValueArgs = ["--policy"];

    [TestCaseSource(nameof(LegacyFallThroughCases))]
    public void Host_LegacyRenderer_NonHelpArgs_FallsThroughToInvocation(string[] args)
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(args);

        // The fake root command defines no options, so parsing yields a parse error
        // rather than a legacy Success response.
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    private static IEnumerable<string[]> LegacyFallThroughCases()
    {
        yield return _policyThenFlagArgs;
        yield return _unknownLeadingTokenArgs;
        yield return _danglingPolicyValueArgs;
    }

    [Test]
    public void Host_TopLevelCommand_SkipsLegacyRendering()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_graphUnknownOptionArgs);

        // "graph" is a recognized top-level command, so legacy root rendering does not apply.
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain("graph --help"));
    }

    private sealed class RootCommandFactory : ICliRootCommandFactory
    {
        public Command Create()
        {
            var root = new RootCommand();
            root.Subcommands.Add(new Command("graph"));
            Command topology = new("topology");
            topology.Subcommands.Add(new Command("capture"));
            topology.Subcommands.Add(new Command("diff"));
            topology.Subcommands.Add(new Command("verify"));
            root.Subcommands.Add(topology);
            return root;
        }
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string OutputText => _output.ToString();
        public string ErrorText => _error.ToString();
    }

    private sealed class RecordingRuntime : ICliRuntime
    {
        private static readonly ArchitectureDependencyGraph _emptyGraph = new(Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>());
        public string Version => "1.0.0";
        public string GraphText { get; init; } = "{}";
        public Exception? GraphException { get; init; }
        public ArchitectureGraphRequest? GraphRequest { get; private set; }
        public ArchitectureExplainOutcome ExplainResult { get; init; } = new("Source", "Target", null, Array.Empty<string>());
        public Exception? ExplainException { get; init; }
        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => Enum.TryParse(value, true, out level);
        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
        {
            GraphRequest = request;
            return GraphException is null ? new ArchitectureGraphOutcome(_emptyGraph) : throw GraphException;
        }
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => GraphText;
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => GraphText;
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => GraphText;
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => ExplainException == null ? ExplainResult : throw ExplainException;
        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();
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
}
