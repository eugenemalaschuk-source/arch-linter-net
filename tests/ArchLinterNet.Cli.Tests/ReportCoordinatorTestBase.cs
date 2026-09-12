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

internal abstract class ReportCoordinatorTestBase
{
    protected static readonly string[] _value = { "one.json", "two.sarif" };
    protected static readonly string[] _value1 = { "bad.json" };
    protected static readonly string[] _value2 = { "first.json", "second.sarif" };
    protected static ValidationOutcome PassedOutcome => new(
        true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
        Array.Empty<ArchitectureViolation>(), "off", Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    protected static ValidationOutcome FailedOutcome => new(
        false,
        new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
        Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
        Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    protected sealed class ThrowingConsole : ICliConsole
    {
        public TextWriter Out { get; } = new StringWriter();
        public TextWriter Error => throw new InvalidOperationException("stream closed");
    }

    protected sealed class CapturingConsole : ICliConsole
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

    protected sealed class StubFileSystem : IFileSystem
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

    protected sealed class FailingFileSystem : IFileSystem
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

    protected sealed class CountingRuntime : ICliRuntime
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

    protected sealed class InvalidJsonRuntime : ICliRuntime
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
