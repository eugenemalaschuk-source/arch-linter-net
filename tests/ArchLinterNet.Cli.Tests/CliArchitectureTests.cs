using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.Contracts;
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
        ValidateCommandHandler handler = new(runtime, console);

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

    [Test]
    public void ValidateHandler_WritesTypedPolicyFailureAsJson()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.SourceShape,
                "Invalid namespace.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain))
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", "json", [], null, false, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("architecture_policy_error"));
            Assert.That(console.StdOut, Does.Contain("architecture/root.yml"));
            Assert.That(console.StdOut, Does.Contain("policy_location"));
            Assert.That(console.StdOut, Does.Contain("source_path"));
            Assert.That(console.StdOut, Does.Contain("source_ordinal"));
            Assert.That(console.StdOut, Does.Contain("import_chain"));
            Assert.That(console.StdOut, Does.Not.Contain("SourcePath"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_UntypedExecutionError_WritesStructuredJsonNotPlainStderr()
    {
        // An expression evaluation failure (e.g. a `when` predicate failing at check time, well
        // after policy load succeeds) surfaces as a bare InvalidOperationException with no
        // ArchitecturePolicyDiagnostic - it must still respect --format json instead of degrading
        // to an unstructured stderr line, so JSON-consuming CI tooling gets parseable output.
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new InvalidOperationException(
                "Contextual selector (role: DomainLayer) 'when' expression failed to evaluate: missing key")
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", "json", [], null, false, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("architecture_execution_error"));
            Assert.That(console.StdOut, Does.Contain("failed to evaluate"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_UntypedExecutionError_WritesStructuredSarifNotPlainStderr()
    {
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new InvalidOperationException("'when' expression failed to evaluate: missing key")
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", "sarif", [], null, false, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("architecture-execution"));
            Assert.That(console.StdOut, Does.Contain("failed to evaluate"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_WritesTypedRootPolicyFailureAsSarif()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.SourceShape,
                "Root policy is not a valid mapping document.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain))
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", "sarif", [], null, false, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("architecture-policy"));
            Assert.That(console.StdOut, Does.Contain("architecture/root.yml"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_WritesRelatedPolicyLocationsAsSarif()
    {
        ArchitecturePolicySourceDescriptor root = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceDescriptor fragment = new(
            "architecture/root.yml", "architecture/parts/domain.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "parts/domain.yml", ["architecture/root.yml", "architecture/parts/domain.yml"]);
        ArchitecturePolicySourceLocation primary = new(root, "layers.domain", 2, 1, null, null);
        ArchitecturePolicySourceLocation related = new(fragment, "layers.domain", 4, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.CompositionConflict,
                "Duplicate layer.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.CompositionConflict, primary, [related], root.ImportChain))
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console);

        handler.Execute(new ValidateCommandOptions("policy.yml", "strict", "sarif", [], null, false, null, false, false));

        using JsonDocument document = JsonDocument.Parse(console.StdOut);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement relatedLocations = result.GetProperty("relatedLocations");
        JsonElement rootLocation = relatedLocations[0];
        JsonElement fragmentLocation = relatedLocations[1];

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("locations")[0].GetProperty("physicalLocation")
                .GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("architecture/root.yml"));
            Assert.That(relatedLocations.GetArrayLength(), Is.EqualTo(2));
            Assert.That(rootLocation.GetProperty("message").GetProperty("text").GetString(),
                Is.EqualTo("Policy root definition at layers.domain"));
            Assert.That(rootLocation.GetProperty("physicalLocation").GetProperty("artifactLocation")
                .GetProperty("uri").GetString(), Is.EqualTo("architecture/root.yml"));
            Assert.That(fragmentLocation.GetProperty("message").GetProperty("text").GetString(),
                Is.EqualTo("Policy fragment definition at layers.domain"));
            Assert.That(fragmentLocation.GetProperty("physicalLocation").GetProperty("artifactLocation")
                .GetProperty("uri").GetString(), Is.EqualTo("architecture/parts/domain.yml"));
        });
    }

    private sealed class FakeCliRuntime : ICliRuntime
    {
        public string Version => "1.2.3";

        public ValidationRequest? LastValidationRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
        {
            level = ArchitectureGraphLevel.Namespace;
            return true;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            LastValidationRequest = request;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

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
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
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
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings)
        {
            throw new NotSupportedException();
        }

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
        {
            throw new NotSupportedException();
        }

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings)
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
