using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class ReportCoordinatorTests
{
    private static readonly string[] _value = { "one.json", "two.sarif" };
    private static readonly string[] _value1 = { "bad.json" };
    private static readonly string[] _value2 = { "first.json", "second.sarif" };
    private static ValidationOutcome PassedOutcome => new(
        true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
        Array.Empty<ArchitectureViolation>(), "off", Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    private static ValidationOutcome FailedOutcome => new(
        false,
        new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
        Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
        Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    [Test]
    public void RouteSingleOutcome_HumanFormatNoAdditionalSinks_WritesHumanToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, []);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(result.FailedPaths, Is.Empty);
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
        Assert.That(console.ErrorText, Is.Empty);
    }

    [Test]
    public void RouteSingleOutcome_JsonFormatNoAdditionalSinks_WritesJsonToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("json", "strict", PassedOutcome, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RouteSingleOutcome_SarifFormatNoAdditionalSinks_WritesSarifToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("sarif", "strict", PassedOutcome, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("version"));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RouteSingleOutcome_HumanWithStderrSink_WritesHumanToBothStdoutAndStderr()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("human", ReportDestinationType.Stderr, null) };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Is.Empty);
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
    }

    [Test]
    public void RouteSingleOutcome_JsonSinkWithHumanStdout_WritesTempThenRenames()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.File, "output.json") };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(fileSystem.TempPaths, Does.Contain("output.json"));
        Assert.That(fileSystem.TargetPaths, Does.Contain("output.json"));
    }

    [Test]
    public void RouteSingleOutcome_DifferentFormats_FormatMethodsCalledOnceEach()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.Stderr, null),
            new ReportSink("sarif", ReportDestinationType.File, "results.sarif"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(runtime.HumanCallCount, Is.EqualTo(0));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(1));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(1));
    }

    [Test]
    public void LegacyCombinedHuman_WritesEachModeSequentiallyWithoutHeaders()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
        Assert.That(console.OutputText, Does.Not.Contain("=== Mode:"));
    }

    [Test]
    public void RouteCombinedOutcomes_ReportModeWithHumanStdout_WritesCombinedHuman()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        var sinks = new[] { new ReportSink("human", ReportDestinationType.Stdout, null) };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("=== Mode: strict ==="));
        Assert.That(console.OutputText, Does.Contain("=== Mode: audit ==="));
    }

    [Test]
    public void RouteCombinedOutcomes_DifferentFormats_FormatMethodsCalledPerMode()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "results.json"),
            new ReportSink("sarif", ReportDestinationType.File, "results.sarif"),
        };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(2));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(2));
    }

    [Test]
    public void RouteSingleOutcome_UnwriteableFileSink_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new FailingFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.File, "output.json") };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("output.json"));
        Assert.That(result.ErrorDetails, Is.Not.Empty);
    }

    [Test]
    public void RouteSingleOutcome_OneFileSinkFailsPhase2_ReturnsPartialOutput()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "bad.sarif"),
        };
        fileSystem.MakeUnwritable("bad.sarif", phase: StubFileSystem.FailPhase.Rename);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
        Assert.That(result.FailedPaths, Does.Contain("bad.sarif"));
        Assert.That(result.FailedPaths, Does.Not.Contain("good.json"));
    }

    [Test]
    public void RouteSingleOutcome_TempWriteFailsForAll_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "one.json"),
            new ReportSink("sarif", ReportDestinationType.File, "two.sarif"),
        };
        fileSystem.MakeUnwritable("one.json", phase: StubFileSystem.FailPhase.Write);
        fileSystem.MakeUnwritable("two.sarif", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Is.EquivalentTo(_value));
    }

    [Test]
    public void RouteSingleOutcome_FirstTempWriteFailsAllRenamesSkipped_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "bad.json"),
            new ReportSink("sarif", ReportDestinationType.File, "good.sarif"),
        };
        fileSystem.MakeUnwritable("bad.json", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        // Phase 1: bad.json fails, good.sarif temp written.
        // Phase 2: skipped entirely.
        // No file was renamed → no output published → OutputFailed, not PartialOutput.
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Is.EquivalentTo(_value1));
    }


    [Test]
    public void ReportMode_StdoutSink_WritesFormatToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.Stdout, null) };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
    }

    [Test]
    public void ReportMode_StderrAndFileSinks_RouteToRespectiveDestinations()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("human", ReportDestinationType.Stderr, null),
            new ReportSink("json", ReportDestinationType.File, "results.json"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Is.Empty);
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
        Assert.That(fileSystem.TempPaths, Does.Contain("results.json"));
    }

    [Test]
    public void Phase2Failure_TracksCommittedAndUncommitted()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "first.json"),
            new ReportSink("sarif", ReportDestinationType.File, "second.sarif"),
        };
        fileSystem.MakeUnwritable("second.sarif", phase: StubFileSystem.FailPhase.Rename);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
        Assert.That(result.CommittedPaths, Does.Contain("first.json"));
        Assert.That(result.FailedPaths, Does.Contain("second.sarif"));
        Assert.That(result.StagedPaths, Is.EquivalentTo(_value2));
    }

    [Test]
    public void SarifFileSink_ValidatesJsonBeforeWrite()
    {
        var runtime = new InvalidJsonRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("sarif", ReportDestinationType.File, "results.sarif") };

        var result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("results.sarif"));
    }

    [Test]
    public void ReportMode_AllFileSinksFail_ReturnsOutputFailedWithStagedPaths()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "a.json"),
            new ReportSink("sarif", ReportDestinationType.File, "b.sarif"),
        };
        fileSystem.MakeUnwritable("a.json", phase: StubFileSystem.FailPhase.Write);
        fileSystem.MakeUnwritable("b.sarif", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.StagedPaths, Is.Empty);
        Assert.That(result.CommittedPaths, Is.Empty);
    }

    [Test]
    public void ReportMode_SingleModeAllSinkTypes_CompletesSuccessfully()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.Stdout, null),
            new ReportSink("human", ReportDestinationType.Stderr, null),
            new ReportSink("sarif", ReportDestinationType.File, "report.sarif"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
        Assert.That(fileSystem.TempPaths, Does.Contain("report.sarif"));
    }

    [Test]
    public void PostWriteTempFileMissing_FailsBeforeAnyRename()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "vanished.sarif"),
        };
        fileSystem.MakeUnwritable("vanished.sarif", phase: StubFileSystem.FailPhase.PostWriteMissing);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        // The temp file for vanished.sarif is reported missing during staging (phase 1), so phase 2
        // never runs for either sink — good.json must not have been committed despite writing fine.
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("vanished.sarif"));
        Assert.That(result.CommittedPaths, Is.Empty);
        Assert.That(fileSystem.TargetPaths, Is.Empty);
    }

    [Test]
    public void PostWriteTempFileCorrupted_FailsBeforeAnyRename()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "corrupted.sarif"),
        };
        fileSystem.MakeUnwritable("corrupted.sarif", phase: StubFileSystem.FailPhase.PostWriteCorrupt);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("corrupted.sarif"));
        Assert.That(result.CommittedPaths, Is.Empty);
        Assert.That(fileSystem.TargetPaths, Is.Empty);
    }

    [Test]
    public void RouteErrorToAllSinks_WritesToFileStdoutAndStderr()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "error.json"),
            new ReportSink("human", ReportDestinationType.Stderr, null),
        };
        var contentByFormat = new Dictionary<string, string>
        {
            ["json"] = "{\"kind\":\"architecture_policy_error\"}",
            ["human"] = "Architecture validation error: bad policy",
        };

        RouteResult result = coordinator.RouteErrorToAllSinks(sinks, contentByFormat);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(fileSystem.TargetPaths, Does.Contain("error.json"));
        Assert.That(console.ErrorText, Does.Contain("bad policy"));
        Assert.That(console.OutputText, Is.Empty);
    }

    [Test]
    public void StreamWriteFailure_IsCaughtLikeAFileFailure_AndDoesNotAbortStaging()
    {
        // A broken stdout/stderr write (closed handle, broken pipe) must not propagate uncaught —
        // that would skip phase 2 entirely and leave whatever an earlier sink in the same batch
        // already staged as an orphaned .tmp file.
        var runtime = new CountingRuntime();
        var console = new ThrowingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("human", ReportDestinationType.Stderr, null),
        };

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("<stderr>"));
        Assert.That(result.ErrorDetails, Does.Contain("stream closed"));
        // The json file sink was staged before the stderr write failed; phase 1 failing must
        // discard that staged temp rather than commit it, and must not throw.
        Assert.That(result.CommittedPaths, Is.Empty);
    }

    private sealed class ThrowingConsole : ICliConsole
    {
        public TextWriter Out { get; } = new StringWriter();
        public TextWriter Error => throw new InvalidOperationException("stream closed");
    }

    private sealed class CapturingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public Action? OnOutputWriteLine { get; init; }
        public TextWriter Out => new CallbackStringWriter(_output, OnOutputWriteLine);
        public TextWriter Error => new StringWriter(_error);
        public string OutputText => _output.ToString();
        public string ErrorText => _error.ToString();

        private sealed class CallbackStringWriter(StringBuilder builder, Action? onWriteLine) : StringWriter(builder)
        {
            public override void WriteLine(string? value)
            {
                base.WriteLine(value);
                onWriteLine?.Invoke();
            }
        }
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public enum FailPhase { Write, Rename, PostWriteMissing, PostWriteCorrupt }

        private readonly record struct FailEntry(string Path, FailPhase Phase);
        private readonly HashSet<FailEntry> _failOn = new();
        private readonly Dictionary<string, string> _tempContents = new();

        public List<string> TempPaths { get; } = new();
        public List<string> TargetPaths { get; } = new();

        // Issue #375: lets a test observe mid-commit cancellation by cancelling the token right
        // after a specific target has been renamed, so the next pending rename in the loop sees
        // IsCancellationRequested at its own top-of-loop check.
        public Action<string>? OnRenamed { get; set; }

        public void MakeUnwritable(string path, FailPhase phase = FailPhase.Write) =>
            _failOn.Add(new FailEntry(path, phase));

        public bool FileExists(string path) => _tempContents.ContainsKey(path);

        public string ReadAllText(string path) => _tempContents.TryGetValue(path, out string? content) ? content : string.Empty;

        public void WriteAllText(string path, string contents) { }

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            if (_failOn.Contains(new FailEntry(targetPath, FailPhase.Write)))
            {
                throw new IOException($"Cannot write to {targetPath}");
            }

            TempPaths.Add(targetPath);
            string tempPath = targetPath + ".tmp";

            // Simulates a temp file that WriteAllTextToTemp reports as created but that never
            // actually landed on disk (or landed with different bytes) — exercises the post-write
            // existence/content re-validation independently of the caller's pre-write checks.
            if (_failOn.Contains(new FailEntry(targetPath, FailPhase.PostWriteMissing)))
            {
                return tempPath;
            }

            _tempContents[tempPath] = _failOn.Contains(new FailEntry(targetPath, FailPhase.PostWriteCorrupt))
                ? "not valid json"
                : contents;

            return tempPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            string original = tempPath.EndsWith(".tmp") ? tempPath[..^4] : tempPath;
            if (_failOn.Contains(new FailEntry(original, FailPhase.Rename)))
            {
                throw new IOException($"Cannot rename to {targetPath}");
            }

            TargetPaths.Add(targetPath);
            OnRenamed?.Invoke(targetPath);
        }

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath)
        {
            if (FileExists(targetPath))
            {
                return false;
            }

            RenameTempToTarget(tempPath, targetPath);
            return true;
        }

        public void DeleteFile(string path)
        {
            _tempContents.Remove(path);
        }

        public bool TryCreateNewFile(string path) => true;

        public bool DirectoryExists(string path) => true;

        public void DeleteDirectoryIfEmpty(string path) { }

        public bool CanWriteToDirectory(string path) => !_failOn.Contains(new FailEntry(path, FailPhase.Write));
    }

    private sealed class FailingFileSystem : IFileSystem
    {
        public bool FileExists(string path) => false;
        public string ReadAllText(string path) => string.Empty;
        public void WriteAllText(string path, string contents) { }
        public string WriteAllTextToTemp(string targetPath, string contents) => throw new IOException("Disk full");
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => false;
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class CountingRuntime : ICliRuntime
    {
        public int HumanCallCount { get; private set; }
        public int JsonCallCount { get; private set; }
        public int SarifCallCount { get; private set; }

        /// <summary>Invoked from FormatViolationsForHumans/FormatResultForCiArtifacts — lets a
        /// test simulate cancellation observed mid-render, between rendering boundaries
        /// ReportCoordinator itself controls (sections within one mode, or modes within a
        /// combined strict+audit report).</summary>
        public Action? OnFormatViolationsForHumans { get; set; }

        public Action? OnFormatResultForCiArtifacts { get; set; }

        public string Version => "1.2.3";

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode, bool passed,
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
            JsonCallCount++;
            OnFormatResultForCiArtifacts?.Invoke();
            return "{\"kind\":\"validation\",\"passed\":true}";
        }

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            SarifCallCount++;
            return "{\"version\":\"2.1.0\",\"runs\":[]}";
        }

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            string.Empty;

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
        {
            HumanCallCount++;
            OnFormatViolationsForHumans?.Invoke();
            return "violations";
        }
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) { HumanCallCount++; return "cycles"; }
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => "pc";
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => "unmatched";
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => "coverage";
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => "summary";
        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => "classifications";

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();
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

    private sealed class InvalidJsonRuntime : ICliRuntime
    {
        public string Version => "1.2.3";
        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode, bool passed,
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
            return "{\"kind\":\"validation\",\"passed\":true}";
        }

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            return "not valid sarif json at all";
        }

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => string.Empty;
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => string.Empty;
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => string.Empty;
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => string.Empty;
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => string.Empty;
        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => string.Empty;
        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();
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
}
