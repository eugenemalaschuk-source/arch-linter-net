using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Issue #374: --profile is opt-in and independent of --timings/--report. See
// openspec/specs/analysis-profile/spec.md, "CLI exposes the profile via a dedicated opt-in
// option". FakeCliRuntime (shared with CliArchitectureTests) does not override ValidateWithCounters,
// so it returns zero-valued counters via ICliRuntime's default implementation — these tests assert
// wiring/shape/opt-in behavior, not real counter values (those are covered by the Testing API
// integration tests in ArchLinterNet.Core.Tests, which go through the real application service).
[TestFixture]
public sealed class ValidateCommandHandlerProfileTests
{
    private sealed class FakeCliRuntime : ICliRuntime
    {
        public string Version => "1.2.3";

        public Exception? ExceptionToThrow { get; init; }

        // Empty by default (every pre-existing test keeps its original "no discovered projects"
        // behavior). Set to a non-empty list to exercise real (non-zero) cache population counters
        // — see Execute_CacheAuto_WithDiscoveredProjects_ReportsNonZeroIneligibleUnitCount below.
        public string[] DiscoveredProjectPaths { get; init; } = Array.Empty<string>();

        // Must point at a real, readable file whenever DiscoveredProjectPaths is non-empty — the
        // cache key's policy digest reads this file's content, and a nonexistent policy path would
        // make population fail with an IOException before ever reaching real per-project eligibility
        // counting (defeating the point of the test this supports).
        public IReadOnlyList<string> PolicyImportPaths { get; init; } = Array.Empty<string>();

        // Finding #8 support: lets a test inject a lookup-side reject (e.g. a corrupt cache entry)
        // without needing a real cache root/entry on disk — ValidateWithCounters below returns it
        // verbatim as part of ArchitectureAnalysisSnapshotCounters.CacheLookups.
        public ArchLinterNet.Core.Caching.AnalysisCacheLookupStats? CacheLookupStats { get; init; }

        public (ValidationOutcome Outcome, ArchitectureAnalysisSnapshotCounters Counters) ValidateWithCounters(
            ValidationRequest request, ValidationTiming? timing)
        {
            ValidationOutcome outcome = Validate(request, timing);
            ArchLinterNet.Core.Caching.AnalysisCacheLookupStats? lookupStats = CacheLookupStats;
            if (lookupStats is null && request.CacheLocation is not null)
            {
                lookupStats = new ArchLinterNet.Core.Caching.AnalysisCacheLookupStats();
                lookupStats.RecordLookup(
                    ArchLinterNet.Core.Caching.AnalysisCacheLookupResult.Reject(
                        ArchLinterNet.Core.Caching.AnalysisCacheRejectReason.IneligibleBuildInput),
                    DiscoveredProjectPaths.Length);
            }

            ArchitectureAnalysisSnapshotCounters counters = new() { CacheLookups = lookupStats };
            return (outcome, counters);
        }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
        {
            level = ArchitectureGraphLevel.Namespace;
            return true;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
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
                ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
            {
                DiscoveredProjectPaths = DiscoveredProjectPaths,
                PolicyImportPaths = PolicyImportPaths,
                RepositoryRoot = Path.GetTempPath(),
            };
        }

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
            IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
            IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) =>
            "{\"status\":\"passed\"}";

        public string FormatResultAsSarif(
            string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) =>
            "{\"version\":\"2.1.0\",\"runs\":[]}";

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) =>
            $"{violations.Count} violation(s)";

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) =>
            $"{cycles.Count} cycle(s)";

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) =>
            throw new NotSupportedException();

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) =>
            throw new NotSupportedException();

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) =>
            throw new NotSupportedException();

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) =>
            throw new NotSupportedException();

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) =>
            throw new NotSupportedException();

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            throw new NotSupportedException();

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

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }

    private static ValidateCommandOptions BaseOptions(string? profileDestination) =>
        new("policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            ProfileDestination = profileDestination,
        };

    // Issue #365: --cache is opt-in and independent of --profile/--timings/--report — see
    // openspec/specs/analysis-cache/spec.md. FakeCliRuntime's default ValidationOutcome has no
    // DiscoveredProjectPaths, so population always reports IneligibleBuildInput here; this proves
    // wiring/shape, not real project-manifest eligibility (see AnalysisCachePopulationTests for
    // that, against a real EvaluatedBuildInputManifestCollector).
    private static ValidateCommandOptions CacheOptions(string? cacheDestination, string? profileDestination = "stdout") =>
        new("policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            ProfileDestination = profileDestination,
            CacheDestination = cacheDestination,
        };

    [Test]
    public void Execute_CacheOmitted_DoesNotChangeBehavior()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(BaseOptions(profileDestination: null));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.StdErr, Is.Empty);
    }

    [Test]
    public void Execute_CacheAuto_ReportsActiveModeAndIneligibleRejectInProfile()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(CacheOptions(cacheDestination: "auto"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success), console.StdErr);
        string[] lines = console.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using JsonDocument document = JsonDocument.Parse(lines[^1]);
        JsonElement cache = document.RootElement.GetProperty("Counters").GetProperty("Cache");
        Assert.Multiple(() =>
        {
            Assert.That(cache.GetProperty("Status").GetString(), Is.EqualTo("Active"));
            Assert.That(cache.GetProperty("Mode").GetString(), Is.EqualTo("auto"));
            Assert.That(cache.GetProperty("Rejects").GetInt32(), Is.EqualTo(1));
            Assert.That(cache.GetProperty("Writes").GetInt32(), Is.EqualTo(0));
            Assert.That(cache.GetProperty("RejectReasonCounts").GetProperty("IneligibleBuildInput").GetInt32(), Is.EqualTo(1));
        });
    }

    // Review finding #6: "only Writes and Rejects are populated ... a run that rejects N projects
    // as ineligible still reports IneligibleUnitCount = 0". With a real (non-empty)
    // DiscoveredProjectPaths, AnalysisCachePopulation.TryPopulate now recomputes a manifest per
    // project and reports how many were ineligible — that count now reaches the profile.
    [Test]
    public void Execute_CacheAuto_WithDiscoveredProjects_ReportsNonZeroIneligibleUnitCount()
    {
        string projectDir = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-cache-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        string realProjectPath = Path.Combine(projectDir, "Sample.csproj");
        File.WriteAllText(
            realProjectPath,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        try
        {
            FakeCliConsole console = new();
            FakeCliRuntime runtime = new()
            {
                DiscoveredProjectPaths = new[] { realProjectPath },
                PolicyImportPaths = new[] { realProjectPath },
            };
            ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

            int exitCode = handler.Execute(CacheOptions(cacheDestination: "auto"));

            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success), console.StdErr);
            string[] lines = console.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            using JsonDocument document = JsonDocument.Parse(lines[^1]);
            JsonElement cache = document.RootElement.GetProperty("Counters").GetProperty("Cache");
            Assert.Multiple(() =>
            {
                Assert.That(cache.GetProperty("IneligibleUnitCount").GetInt32(), Is.GreaterThan(0));
                Assert.That(cache.GetProperty("Writes").GetInt32(), Is.EqualTo(0));
                Assert.That(cache.GetProperty("BytesWritten").GetInt64(), Is.EqualTo(0));
            });
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    // Finding #8: the scalar Rejects counter must aggregate both population-side rejects (this
    // cache lookup rejects. The test runtime injects both a Corrupt and an
    // IneligibleBuildInput lookup to verify that the profile scalar totals their real observed
    // reasons; it must not recreate authorization after analysis merely to manufacture a
    // population-side reject.
    [Test]
    public void Execute_CacheAuto_WithCorruptAndIneligibleLookups_AggregatesRejectsAcrossBothSides()
    {
        ArchLinterNet.Core.Caching.AnalysisCacheLookupStats lookupStats = new();
        lookupStats.RecordLookup(ArchLinterNet.Core.Caching.AnalysisCacheLookupResult.Reject(
            ArchLinterNet.Core.Caching.AnalysisCacheRejectReason.Corrupt, bytesRead: 128));
        lookupStats.RecordLookup(ArchLinterNet.Core.Caching.AnalysisCacheLookupResult.Reject(
            ArchLinterNet.Core.Caching.AnalysisCacheRejectReason.IneligibleBuildInput));

        FakeCliConsole console = new();
        FakeCliRuntime runtime = new() { CacheLookupStats = lookupStats };
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(CacheOptions(cacheDestination: "auto"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success), console.StdErr);
        string[] lines = console.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using JsonDocument document = JsonDocument.Parse(lines[^1]);
        JsonElement cache = document.RootElement.GetProperty("Counters").GetProperty("Cache");
        Assert.Multiple(() =>
        {
            Assert.That(cache.GetProperty("Rejects").GetInt32(), Is.EqualTo(2), "two lookup rejects");
            Assert.That(cache.GetProperty("RejectReasonCounts").GetProperty("Corrupt").GetInt32(), Is.EqualTo(1));
            Assert.That(cache.GetProperty("RejectReasonCounts").GetProperty("IneligibleBuildInput").GetInt32(), Is.EqualTo(1));
            Assert.That(cache.GetProperty("CorruptionEvents").GetInt32(), Is.EqualTo(1));

            int sumOfReasonCounts = cache.GetProperty("RejectReasonCounts").EnumerateObject().Sum(p => p.Value.GetInt32());
            Assert.That(cache.GetProperty("Rejects").GetInt32(), Is.EqualTo(sumOfReasonCounts));
        });
    }

    [Test]
    public void Execute_CacheUnsafePath_IsRejectedBeforeAnalysis()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));
        string unsafePath = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";

        int exitCode = handler.Execute(CacheOptions(cacheDestination: unsafePath, profileDestination: null));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Cannot use --cache"));
        });
    }

    [Test]
    public void Execute_ProfileOmitted_WritesNothingExtraAndBehavesUnchanged()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(BaseOptions(profileDestination: null));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void Execute_ProfileStdout_WritesDeterministicJsonDocumentAfterReport()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(BaseOptions(profileDestination: "stdout"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));

        // Stdout carries the human report (first line) followed by the profile JSON document
        // (last line) — --profile never replaces --format's own output.
        string[] lines = console.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Has.Length.GreaterThanOrEqualTo(2));

        using JsonDocument document = JsonDocument.Parse(lines[^1]);
        JsonElement root = document.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
            Assert.That(root.GetProperty("CompletionStatus").GetString(), Is.EqualTo("Success"));
            Assert.That(root.GetProperty("CancellationObserved").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("Counters").GetProperty("RenderedSinkCount").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("Counters").GetProperty("OutputSinkCount").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("Counters").GetProperty("Cache").GetProperty("Status").GetString(),
                Is.EqualTo("NotApplicable"));
        });
    }

    [Test]
    public void Execute_ProfileStderr_DoesNotPolluteStdout()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(BaseOptions(profileDestination: "stderr"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.StdOut.Trim(), Is.EqualTo("Architecture validation passed."));
        Assert.That(console.StdErr, Does.Contain("\"SchemaId\""));
    }

    [Test]
    public void Execute_ProfileFilePath_WritesJsonDirectlyWithoutStagingTempFile()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, fileSystem);

        int exitCode = handler.Execute(BaseOptions(profileDestination: "profile.json"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        Assert.That(fileSystem.DirectWrites, Contains.Key("profile.json"));
        using JsonDocument document = JsonDocument.Parse(fileSystem.DirectWrites["profile.json"]);
        Assert.That(document.RootElement.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
    }

    [Test]
    public void Execute_ProfileWithoutTimings_DoesNotAlsoPrintHumanTimingReport()
    {
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(BaseOptions(profileDestination: "stderr"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.StdErr, Does.Not.Contain("Validation timings:"));
    }

    [Test]
    public void Execute_ProfileFileDestinationNotWritable_IsRejectedBeforeAnalysis()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true, writable: false);
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, fileSystem);

        int exitCode = handler.Execute(BaseOptions(profileDestination: "profiles/result.json"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Cannot write profile to 'profiles/result.json'"));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
        });
    }

    [Test]
    public void Execute_CancelledDuringAnalysis_WritesCancelledProfile()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(
            new FakeCliRuntime { ExceptionToThrow = new OperationCanceledException("cancelled") },
            console,
            fileSystem);

        int exitCode = handler.Execute(BaseOptions(profileDestination: "cancelled-profile.json"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.DirectWrites, Contains.Key("cancelled-profile.json"));
            using JsonDocument document = JsonDocument.Parse(fileSystem.DirectWrites["cancelled-profile.json"]);
            Assert.That(document.RootElement.GetProperty("CompletionStatus").GetString(), Is.EqualTo("Cancelled"));
            Assert.That(document.RootElement.GetProperty("CancellationObserved").GetBoolean(), Is.True);
            Assert.That(document.RootElement.GetProperty("Output").GetProperty("OutputFailed").GetBoolean(), Is.False);
        });
    }

    [Test]
    public void Execute_CancelledAfterDynamicInputDiscovery_DoesNotOverwriteThatInput()
    {
        OperationCanceledException cancellation = new("cancelled");
        cancellation.Data["ArchLinterNet.AnalysisProfile.Counters"] =
            new ArchitectureAnalysisSnapshotCounters { PolicyCompositions = 1, AssemblyLoads = 1 };
        cancellation.Data["ArchLinterNet.AnalysisProfile.InputPaths"] = new[] { Path.GetFullPath("imported-policy.yml") };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(
            new FakeCliRuntime { ExceptionToThrow = cancellation }, console, fileSystem);

        int exitCode = handler.Execute(BaseOptions(profileDestination: "imported-policy.yml"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
            Assert.That(console.StdErr, Does.Contain("profile was not written"));
        });
    }

    [Test]
    public void Execute_ReportOutputFailure_RecordsActualPublicationResultInProfile()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, fileSystem);
        ValidateCommandOptions options = BaseOptions(profileDestination: "profile.json") with
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "report.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        using JsonDocument document = JsonDocument.Parse(fileSystem.DirectWrites["profile.json"]);
        JsonElement output = document.RootElement.GetProperty("Output");
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("CompletionStatus").GetString(),
                Is.EqualTo("Success"));
            Assert.That(document.RootElement.GetProperty("Counters").GetProperty("RenderedSinkCount").GetInt32(),
                Is.EqualTo(1));
            Assert.That(output.GetProperty("OutputFailed").GetBoolean(), Is.True);
            Assert.That(output.GetProperty("FailedSinkCount").GetInt32(), Is.EqualTo(1));
            Assert.That(output.GetProperty("CommittedSinkCount").GetInt32(), Is.EqualTo(0));
            Assert.That(output.GetProperty("UncommittedSinkCount").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public void Execute_ProfileDestinationMatchingPolicy_IsRejectedBeforeWriting()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, fileSystem);

        int exitCode = handler.Execute(BaseOptions(profileDestination: "policy.yml"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("--profile destination 'policy.yml' matches an input file"));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
        });
    }

    [Test]
    public void Execute_ProfileDestinationMatchingReport_IsRejectedBeforeWriting()
    {
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(new FakeCliRuntime(), console, fileSystem);
        ValidateCommandOptions options = BaseOptions(profileDestination: "result.json") with
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "result.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("matches --report destination 'result.json'"));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
        });
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

    private sealed class FakeFileSystem(bool exists, bool writable = true) : IFileSystem
    {
        public Dictionary<string, string> DirectWrites { get; } = new();

        public bool FileExists(string path) => exists;

        public string ReadAllText(string path) => string.Empty;

        public void WriteAllText(string path, string contents)
        {
            DirectWrites[path] = contents;
        }

        public string WriteAllTextToTemp(string targetPath, string contents) => targetPath + ".tmp";

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
        }

        public void DeleteFile(string path)
        {
        }

        public bool CanWriteToDirectory(string path) => writable;
    }
}
