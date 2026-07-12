using System.Text;
using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CliArchitectureTests
{
    [Test]
    public void Composition_ResolvesCliHostAndHandlersWithoutStaticGlobals()
    {
        CliComposition composition = CliCompositionRoot.Compose();

        Assert.Multiple(() =>
        {
            Assert.That(composition.Host, Is.Not.Null);
            Assert.That(composition.RootCommandFactory, Is.Not.Null);
            Assert.That(composition.Runtime, Is.Not.Null);
            Assert.That(composition.RootCommandModule, Is.InstanceOf<ValidateCommandModule>());
            Assert.That(composition.SubcommandModules.Select(static module => module.GetType()), Is.EquivalentTo(new[]
            {
                typeof(BaselineCommandModule),
                typeof(GraphCommandModule),
                typeof(ExplainCommandModule),
            }));
            Assert.That(
                composition.RootCommandFactory.Create().Subcommands.Select(static command => command.Name),
                Is.EquivalentTo(new[] { "baseline", "graph", "explain" }));
        });
    }

    [Test]
    public void BaselineModule_ComposesSubcommandsFromModules()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        BaselineCommandModule module = new();

        var commandNames = module.CreateCommand(runtime, console, fileSystem).Subcommands.Select(static command => command.Name).ToArray();

        Assert.That(commandNames, Is.EquivalentTo(new[] { "generate", "update", "prune", "diff", "verify" }));
    }

    [Test]
    public void ValidateHandler_UsesInjectedServicesAndWritesHumanSuccess()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml",
            "strict",
            "human",
            ["rule-1"],
            "dev",
            TimingsEnabled: false,
            BaselinePath: "baseline.yml",
            ShowHelp: false,
            ShowVersion: false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.LastValidationRequest, Is.Not.Null);
            Assert.That(runtime.LastValidationRequest!.PolicyPath, Is.EqualTo("policy.yml"));
            Assert.That(runtime.LastValidationRequest.ConditionSetName, Is.EqualTo("dev"));
            Assert.That(runtime.LastValidationRequest.BaselinePath, Is.EqualTo("baseline.yml"));
            Assert.That(runtime.LastValidationRequest.ContractIds, Is.EqualTo(new[] { "rule-1" }));
            Assert.That(console.StdOut, Does.Contain("Architecture validation passed."));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    private sealed class FakeCliRuntime : ICliRuntime
    {
        public string Version => "1.2.3";

        public ValidationRequest? LastValidationRequest { get; private set; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
        {
            level = ArchitectureGraphLevel.Namespace;
            return true;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            LastValidationRequest = request;
            return new ValidationOutcome(
                Passed: true,
                Violations: Array.Empty<ArchitectureViolation>(),
                Cycles: Array.Empty<string>(),
                CoverageFindings: Array.Empty<ArchitectureViolation>(),
                CoverageConfig: "off",
                UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
                UnmatchedIgnoredViolationsConfig: "off",
                PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
                PolicyConsistencyConfig: "off",
                CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
                ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
                ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());
        }

        public string FormatResultForCiArtifacts(
            string mode,
            bool passed,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
            IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
            IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
        {
            throw new NotSupportedException();
        }

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
        {
            throw new NotSupportedException();
        }

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles)
        {
            throw new NotSupportedException();
        }

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
        {
            throw new NotSupportedException();
        }

        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
        {
            throw new NotSupportedException();
        }

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics)
        {
            throw new NotSupportedException();
        }

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations)
        {
            throw new NotSupportedException();
        }

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings)
        {
            throw new NotSupportedException();
        }

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries)
        {
            throw new NotSupportedException();
        }

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

    private sealed class FakeCliConsole : ICliConsole
    {
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();

        public TextWriter Out => new StringWriter(_stdout);

        public TextWriter Error => new StringWriter(_stderr);

        public string StdOut => _stdout.ToString();

        public string StdErr => _stderr.ToString();
    }

    private sealed class FakeFileSystem(bool exists) : IFileSystem
    {
        public bool FileExists(string path)
        {
            return exists;
        }

        public void WriteAllText(string path, string contents)
        {
        }
    }
}
