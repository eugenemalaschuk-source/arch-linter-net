using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Split out of CliArchitectureTests (which grew past the file-size lint threshold): every
// ValidateCommandHandler test exercising --report sink routing for policy/execution/output
// errors — the report-mode error-routing surface added and hardened across #364's review
// iterations.
[TestFixture]
public sealed partial class ValidateCommandHandlerReportModeTests
{
    [Test]
    public void ValidateHandler_ReportMode_PolicyErrorRoutesJsonToFileSink()
    {
        // Regression coverage for the report-mode error routing gap: a policy load failure occurs
        // before any outcome exists, so with --report json=out.json configured the JSON-shaped
        // error document must land in out.json itself, not on process stdout.
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Invalid namespace.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain),
                ArchitecturePolicyImportErrorCategory.SourceShape.ToString())
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "out.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Does.Contain("out.json"));
            Assert.That(console.StdOut, Is.Empty);
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_ExecutionErrorRoutesToFileAndStreamSinks()
    {
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new InvalidOperationException("'when' expression failed to evaluate: boom")
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("json", ReportDestinationType.File, "err.json"),
                new ReportSink("human", ReportDestinationType.Stderr, null),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Does.Contain("err.json"));
            Assert.That(console.StdErr, Does.Contain("boom"));
            Assert.That(console.StdOut, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_OutputErrorFallsBackToStderrWhenNoStreamSink()
    {
        // Post-outcome failures cannot safely be written into a File sink (it either just failed,
        // or already committed a legitimate report that must not be overwritten), so with only a
        // File sink configured, the operational diagnostic falls back to process stderr.
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.txt");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("human", ReportDestinationType.File, "broken.txt")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Report output failed"));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_FileFailureReplacesDeferredStderrReportWithOneDiagnostic()
    {
        // File sinks now complete phase 1 before a normal stream document is emitted. Therefore a
        // failed file sink can replace the deferred stderr report with one output-failure document
        // rather than leaving stderr with a successful-looking report and exit code 2.
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.txt");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("human", ReportDestinationType.File, "broken.txt"),
                new ReportSink("human", ReportDestinationType.Stderr, null),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Architecture validation passed."));
            Assert.That(console.StdErr, Does.Contain("Report output failed"));
            Assert.That(System.Text.RegularExpressions.Regex.Matches(console.StdErr, "Architecture validation passed.").Count,
                Is.EqualTo(1));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_OutputErrorSarifIsRealSarifNotJsonShape()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.sarif");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("sarif", ReportDestinationType.File, "broken.sarif")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            Assert.That(document.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("architecture-output"));
            Assert.That(result.GetProperty("properties").GetProperty("output_status").GetString(),
                Is.EqualTo("output-failed"));
            Assert.That(console.StdErr, Does.Not.Contain("architecture_execution_error"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_OutputErrorIncludesUnderlyingValidationResult()
    {
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = new ValidationOutcome(
                false,
                new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
                Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
                Array.Empty<PolicyConsistencyDiagnostic>(), "off",
                Array.Empty<ArchitectureCoverageSummary>(),
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>()),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.txt");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            // No stream sink configured, so stderr is a genuinely idle fallback channel and the
            // output-error notice (carrying the full rendered report, not just a summary) actually
            // reaches it.
            AdditionalSinks = [new ReportSink("human", ReportDestinationType.File, "broken.txt")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            // An output-routing failure must not swallow whether the underlying validation
            // itself passed or failed — the exit code alone (2, not 1) can't convey that. It must
            // not reduce it to a bare count either: the actual normalized finding text
            // (FakeCliRuntime.FormatViolationsForHumans) is embedded verbatim.
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("1 violation(s)"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_OutputErrorJsonEmbedsFullReportNotJustCounts()
    {
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = new ValidationOutcome(
                false,
                new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
                Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
                Array.Empty<PolicyConsistencyDiagnostic>(), "off",
                Array.Empty<ArchitectureCoverageSummary>(),
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>()),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.json");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "broken.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            JsonElement report = document.RootElement.GetProperty("report");
            // The full, already-rendered report (FakeCliRuntime.FormatResultForCiArtifacts) is
            // embedded verbatim under "report", not reduced to a bare pass/fail + count summary.
            Assert.That(report.GetProperty("mode").GetString(), Is.EqualTo("strict"));
            Assert.That(report.GetProperty("passed").GetBoolean(), Is.False);
            Assert.That(report.GetProperty("violation_count").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_DoesNotOverwriteDiscoveredProjectFile()
    {
        string projectPath = Path.GetFullPath("src/App/App.csproj");
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = new ValidationOutcome(
                true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
                Array.Empty<ArchitectureCoverageSummary>(), Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>())
            {
                DiscoveredProjectPaths = [projectPath],
            },
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "src/App/App.csproj")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
            Assert.That(console.StdErr, Does.Contain("matches a project file loaded during this run"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_PolicyErrorDoesNotOverwriteFailedImportFragment()
    {
        // A --report destination that matches the exact file this import failure involves must
        // not receive the error document — that would overwrite the fragment that just failed
        // to import with unrelated error JSON.
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/fragment.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "fragment.yml", ["architecture/root.yml", "architecture/fragment.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "imports[0]", 2, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Policy source file not found: architecture/fragment.yml",
                new ArchitecturePolicyDiagnostic(
                    ArchitecturePolicyDiagnosticKind.ImportResolution, location, [], source.ImportChain),
                ArchitecturePolicyImportErrorCategory.MissingFile.ToString()),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "architecture/fragment.yml")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
            Assert.That(console.StdErr, Does.Contain("matches a policy file involved in this import failure"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_GenericExecutionErrorDoesNotOverwriteLoadedAssembly()
    {
        // A generic execution failure (contract execution, expression evaluation) that happens
        // after policy and assembly loading already succeeded still names files this invocation
        // actually consumed via ArchitectureAnalysisEvaluationException — a --report destination
        // matching one of those loaded assemblies must not receive the error document either.
        string assemblyPath = Path.GetFullPath("bin/MyApp.dll");
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitectureAnalysisEvaluationException(
                "'when' expression failed to evaluate: missing key",
                new InvalidOperationException("missing key"),
                Array.Empty<string>(),
                new[] { assemblyPath }),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "bin/MyApp.dll")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
            Assert.That(console.StdErr, Does.Contain("matches a build artifact or receipt loaded during this run"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_ErrorReportWriteFailureSurfacesEvidence()
    {
        // RouteErrorToAllSinks's result must not be discarded: when the error report itself
        // fails to reach its file sink, that failure needs its own typed evidence, not silence.
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Invalid namespace.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain),
                ArchitecturePolicyImportErrorCategory.SourceShape.ToString()),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("unwritable.json");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "unwritable.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            // The fallback remains one valid JSON document; a second human line would corrupt a
            // file-only JSON report consumer's stderr stream.
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture_policy_error"));
            Assert.That(document.RootElement.GetProperty("output_status").GetString(), Is.EqualTo("output-failed"));
            Assert.That(document.RootElement.GetProperty("failed_paths")[0].GetString(), Is.EqualTo("unwritable.json"));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_FileFailureReplacesDeferredJsonStderrWithOutputDiagnostic()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("broken.sarif");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("sarif", ReportDestinationType.File, "broken.sarif"),
                new ReportSink("json", ReportDestinationType.Stderr, null),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            Assert.That(document.RootElement.GetProperty("output_status").GetString(), Is.EqualTo("output-failed"));
            Assert.That(document.RootElement.GetProperty("failed_paths").EnumerateArray()
                .Select(path => path.GetString()), Does.Contain("broken.sarif"));
            Assert.That(document.RootElement.GetProperty("uncommitted_paths").EnumerateArray()
                .Select(path => path.GetString()), Does.Contain("broken.sarif"));
            Assert.That(document.RootElement.GetProperty("errors").GetArrayLength(), Is.GreaterThan(0));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_FailedStdoutPreventsNormalSarifStderrAndFallsBackWithDiagnostics()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new(outputWriteFailures: 1);
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("json", ReportDestinationType.Stdout, null),
                new ReportSink("sarif", ReportDestinationType.Stderr, null),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Is.Empty);
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            Assert.That(document.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            JsonElement routingResult = document.RootElement.GetProperty("runs").EnumerateArray().Last()
                .GetProperty("results")[0];
            Assert.That(routingResult.GetProperty("ruleId").GetString(), Is.EqualTo("architecture-output"));
            JsonElement properties = routingResult.GetProperty("properties");
            Assert.That(properties.GetProperty("output_status").GetString(), Is.EqualTo("output-failed"));
            Assert.That(properties.GetProperty("failed_paths").EnumerateArray()
                .Select(path => path.GetString()), Does.Contain("<stdout>"));
            Assert.That(properties.GetProperty("errors").GetArrayLength(), Is.GreaterThan(0));
        });
    }

    [Test]
    public void ValidateHandler_ReportMode_ErrorFileFailureReplacesDeferredStderrWithOneDiagnostic()
    {
        // The policy error itself is only emitted after its file sink stages. If that staging
        // fails, stderr receives one enriched JSON fallback, not the bare error followed by a
        // second document.
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Invalid namespace.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain),
                ArchitecturePolicyImportErrorCategory.SourceShape.ToString()),
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        fileSystem.FailOnWrite.Add("unwritable.json");
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("json", ReportDestinationType.File, "unwritable.json"),
                new ReportSink("json", ReportDestinationType.Stderr, null),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            using JsonDocument document = JsonDocument.Parse(console.StdErr);
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture_policy_error"));
            Assert.That(document.RootElement.GetProperty("output_status").GetString(), Is.EqualTo("output-failed"));
            Assert.That(document.RootElement.GetProperty("failed_paths")[0].GetString(), Is.EqualTo("unwritable.json"));
        });
    }

    private sealed class FakeCliRuntime : ICliRuntime
    {
        public int ValidationCallCount { get; private set; }

        public string Version => "1.2.3";

        public ValidationRequest? LastValidationRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public ValidationOutcome? ForcedOutcome { get; init; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
        {
            level = ArchitectureGraphLevel.Namespace;
            return true;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            ValidationCallCount++;
            LastValidationRequest = request;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (ForcedOutcome is not null)
            {
                return ForcedOutcome;
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

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

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
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            return JsonSerializer.Serialize(new { mode, passed, violation_count = violations.Count });
        }

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
        {
            throw new NotSupportedException();
        }

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics)
        {
            throw new NotSupportedException();
        }

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            return "{\"version\":\"2.1.0\",\"runs\":[]}";
        }

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
        {
            return $"{violations.Count} violation(s)";
        }

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings)
        {
            return $"{cycles.Count} cycle(s)";
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
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();

        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) =>
            throw ExceptionToThrow ?? new NotSupportedException();

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }

    private sealed class FakeCliConsole(int errorWriteFailures = 0, int outputWriteFailures = 0) : ICliConsole
    {
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();
        private int _errorWriteFailuresRemaining = errorWriteFailures;
        private int _outputWriteFailuresRemaining = outputWriteFailures;

        public TextWriter Out => new FailingStringWriter(_stdout, this, isError: false);

        public TextWriter Error => new FailingStringWriter(_stderr, this, isError: true);

        public string StdOut => _stdout.ToString();

        public string StdErr => _stderr.ToString();

        private bool ConsumeWriteFailure(bool isError)
        {
            if (isError)
            {
                if (_errorWriteFailuresRemaining == 0)
                {
                    return false;
                }

                _errorWriteFailuresRemaining--;
                return true;
            }

            if (_outputWriteFailuresRemaining == 0)
            {
                return false;
            }

            _outputWriteFailuresRemaining--;
            return true;
        }

        private sealed class FailingStringWriter(StringBuilder builder, FakeCliConsole owner, bool isError) : StringWriter(builder)
        {
            public override void WriteLine(string? value)
            {
                if (owner.ConsumeWriteFailure(isError))
                {
                    throw new IOException(isError ? "stderr is closed" : "stdout is closed");
                }

                base.WriteLine(value);
            }
        }
    }

    private sealed class FakeFileSystem(bool exists) : IFileSystem
    {
        private readonly Dictionary<string, string> _tempContents = new();

        public HashSet<string> FailOnWrite { get; } = new();

        public List<string> CommittedPaths { get; } = new();

        public bool FileExists(string path)
        {
            return _tempContents.ContainsKey(path) || exists;
        }

        public string ReadAllText(string path)
        {
            return _tempContents.TryGetValue(path, out string? content) ? content : string.Empty;
        }

        public void WriteAllText(string path, string contents)
        {
        }

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            if (FailOnWrite.Contains(targetPath))
            {
                throw new IOException($"Cannot write to {targetPath}");
            }

            string tempPath = targetPath + ".tmp";
            _tempContents[tempPath] = contents;
            return tempPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            CommittedPaths.Add(targetPath);
        }

        public void DeleteFile(string path)
        {
            _tempContents.Remove(path);
        }

        public bool CanWriteToDirectory(string path) => true;
    }
}
