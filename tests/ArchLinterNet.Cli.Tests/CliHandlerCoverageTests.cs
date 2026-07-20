using System.CommandLine;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Infrastructure;
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

    private static ArchitecturePolicyImportException PolicyException()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.MissingFile,
            "Root policy file not found: architecture/root.yml",
            new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.ImportResolution,
                new ArchitecturePolicySourceLocation(source, "$", 1, 1, null, null),
                [],
                source.ImportChain));
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
        Assert.That(console.ErrorText, Does.Contain(expectedError));
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
    public void Host_LegacyHelp_ShortCircuitsBeforeParsing()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_helpArgs);

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.OutputText, Does.Contain("arch-linter-net — architecture contract linter"));
    }

    [Test]
    public void Host_LegacyVersion_ShortCircuitsBeforeParsing()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_versionArgs);

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.OutputText, Does.Contain("arch-linter-net 1.0.0"));
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

    // The legacy validate short-circuit only intercepts --help/--version; every other
    // combination walks the arg list and returns false, falling through to normal parsing.
    // These cases exercise the value-skipping (--policy <value>), flag (--strict), unknown-token,
    // and dangling-option-value branches of TryHandleLegacyValidateShortCircuit.
    private static readonly string[] _policyThenFlagArgs = ["--policy", "custom.yml", "--strict"];
    private static readonly string[] _unknownLeadingTokenArgs = ["not-an-option"];
    private static readonly string[] _danglingPolicyValueArgs = ["--policy"];

    [TestCaseSource(nameof(LegacyFallThroughCases))]
    public void Host_LegacyShortCircuit_NonHelpArgs_FallThroughToParsing(string[] args)
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(args);

        // The fake root command defines no options, so falling through to normal parsing
        // yields a parse error rather than the legacy Success short-circuit.
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    private static IEnumerable<string[]> LegacyFallThroughCases()
    {
        yield return _policyThenFlagArgs;
        yield return _unknownLeadingTokenArgs;
        yield return _danglingPolicyValueArgs;
    }

    [Test]
    public void Host_TopLevelCommand_SkipsLegacyShortCircuit()
    {
        var console = new RecordingConsole();
        int result = new CliHost(new RootCommandFactory(), console, new RecordingRuntime()).Run(_graphUnknownOptionArgs);

        // "graph" is a recognized top-level command, so the short-circuit returns immediately
        // (IsTopLevelCommand branch) and the arg list is handed straight to the parser.
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain("graph --help"));
    }

    private sealed class RootCommandFactory : ICliRootCommandFactory
    {
        public Command Create()
        {
            var root = new RootCommand();
            root.Subcommands.Add(new Command("graph"));
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
        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();
        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();
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
