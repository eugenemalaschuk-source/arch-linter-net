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

namespace ArchLinterNet.Cli.Tests;

// Split out of ValidateCommandHandlerReportModeTests.cs (which grew past the file-size lint
// threshold) — the shared ICliRuntime/ICliConsole/IFileSystem test doubles every partial file of
// this class uses, kept together as the single-responsibility "fakes" concern the lint failure's
// own guidance calls for.
public sealed partial class ValidateCommandHandlerReportModeTests
{
    private sealed class FakeCliRuntime : ICliRuntime
    {
        public int ValidationCallCount { get; private set; }

        public string Version => "1.2.3";

        public ValidationRequest? LastValidationRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public ValidationOutcome? ForcedOutcome { get; init; }

        // null preserves every existing test's hardcoded empty-runs default; set this to supply a
        // native SARIF document a test needs to assert against (e.g. a native rule/result the CLI's
        // imported-diagnostics merge must not collide with — see ExternalEvidence tests).
        public string? ForcedSarif { get; init; }

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
            return ForcedSarif ?? "{\"version\":\"2.1.0\",\"runs\":[]}";
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

        // Issue #375 PR #416 review: lets a test simulate cancellation racing with (rather than
        // preceding) a real file-system operation — e.g. cancelling right as a temp file write
        // succeeds — without the write itself failing.
        public Action? OnWriteAllTextToTemp { get; set; }

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
            OnWriteAllTextToTemp?.Invoke();
            return tempPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            CommittedPaths.Add(targetPath);
        }

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => !FileExists(targetPath);

        public void DeleteFile(string path)
        {
            _tempContents.Remove(path);
        }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;

        public void DeleteDirectoryIfEmpty(string path) { }

        public bool CanWriteToDirectory(string path) => true;
    }
}
