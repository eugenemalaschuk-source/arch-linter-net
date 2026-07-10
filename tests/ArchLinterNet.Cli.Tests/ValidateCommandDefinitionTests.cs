using System.CommandLine;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ValidateCommandDefinitionTests
{
    [TestCase(new string[0], "strict")]
    [TestCase(new[] { "--mode", "audit" }, "audit")]
    [TestCase(new[] { "-m", "audit" }, "audit")]
    [TestCase(new[] { "--strict" }, "strict")]
    [TestCase(new[] { "--audit" }, "audit")]
    [TestCase(new[] { "--audit", "--strict" }, "strict")]
    [TestCase(new[] { "--mode", "AUDIT" }, "audit")]
    public void CreateRootCommand_ModeOptionCombinations_ResolveExpectedMode(string[] args, string expectedMode)
    {
        (RecordingRuntime runtime, _) = Run(args);

        Assert.That(runtime.LastRequest!.Mode, Is.EqualTo(expectedMode));
    }

    [Test]
    public void CreateRootCommand_UnrecognizedModeToken_IsPassedThroughAndRejectedByHandler()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run(["--mode", "bogus"]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Invalid mode: bogus"));
    }

    [TestCase(new string[0], "human")]
    [TestCase(new[] { "--format", "json" }, "json")]
    [TestCase(new[] { "-f", "sarif" }, "sarif")]
    [TestCase(new[] { "--json" }, "json")]
    [TestCase(new[] { "--format", "JSON" }, "json")]
    public void CreateRootCommand_FormatOptionCombinations_ResolveExpectedFormat(string[] args, string expectedFormat)
    {
        (_, RecordingConsole console) = Run(args, format: expectedFormat);

        Assert.That(console.OutputText, Does.Contain(expectedFormat == "human" ? "Architecture validation passed." : "formatted"));
    }

    [Test]
    public void CreateRootCommand_UnrecognizedFormatToken_IsPassedThroughAndRejectedByHandler()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run(["--format", "bogus"]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Invalid format: bogus"));
    }

    [Test]
    public void CreateRootCommand_NoOptions_UsesDefaultPolicyPath()
    {
        (RecordingRuntime runtime, _) = Run(Array.Empty<string>());

        Assert.That(runtime.LastRequest!.PolicyPath, Is.EqualTo("architecture/dependencies.arch.yml"));
    }

    [Test]
    public void CreateRootCommand_PolicyContractAndConditionSetOptions_ArePropagated()
    {
        (RecordingRuntime runtime, _) = Run([
            "--policy", "custom.arch.yml",
            "--contract", "rule-a",
            "--contract", "rule-b",
            "--condition-set", "ci",
            "--baseline", "baseline.yml",
        ]);

        Assert.That(runtime.LastRequest!.PolicyPath, Is.EqualTo("custom.arch.yml"));
        Assert.That(runtime.LastRequest.ContractIds, Is.EquivalentTo(new[] { "rule-a", "rule-b" }));
        Assert.That(runtime.LastRequest.ConditionSetName, Is.EqualTo("ci"));
        Assert.That(runtime.LastRequest.BaselinePath, Is.EqualTo("baseline.yml"));
    }

    [Test]
    public void CreateRootCommand_TimingsOption_IsPropagated()
    {
        (RecordingRuntime runtime, _) = Run(["--timings"]);

        Assert.That(runtime.LastTiming, Is.Not.Null);
    }

    private static (RecordingRuntime Runtime, RecordingConsole Console) Run(string[] args, string format = "human")
    {
        var runtime = new RecordingRuntime();
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem(true);
        RootCommand command = new ValidateCommandModule().CreateRootCommand(runtime, console, fileSystem);

        List<string> fullArgs = new(args);
        if (format != "human" && !fullArgs.Contains("--format") && !fullArgs.Contains("-f") && !fullArgs.Contains("--json"))
        {
            fullArgs.Add("--format");
            fullArgs.Add(format);
        }

        command.Parse(fullArgs.ToArray()).Invoke();
        return (runtime, console);
    }

    private sealed class RecordingFileSystem(bool exists) : IFileSystem
    {
        public bool FileExists(string path) => exists;
        public void WriteAllText(string path, string contents) { }
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
        public string Version => "1.2.3";
        public ValidationRequest? LastRequest { get; private set; }
        public ValidationTiming? LastTiming { get; private set; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            LastRequest = request;
            LastTiming = timing;
            return new ValidationOutcome(
                true,
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<string>(),
                Array.Empty<ArchitectureViolation>(),
                "off",
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
                "off",
                Array.Empty<PolicyConsistencyDiagnostic>(),
                "off",
                Array.Empty<ArchitectureCoverageSummary>());
        }

        public string FormatResultForCiArtifacts(
            string mode,
            bool passed,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => "formatted";

        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles) => "formatted";
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => "formatted";
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles) => "formatted";
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => "formatted";
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => "formatted";
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => "formatted";
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => "formatted";
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }
}
