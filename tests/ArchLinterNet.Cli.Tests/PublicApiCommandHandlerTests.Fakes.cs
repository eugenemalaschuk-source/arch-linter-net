using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class PublicApiCommandHandlerTests
{
    private sealed class StubFileSystem(params string[] existingPaths) : IFileSystem
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);

        public string? LastWritePath { get; private set; }

        public string? LastWriteContents { get; private set; }

        public string ReadContents { get; init; } = string.Empty;

        public int RenameCount { get; private set; }

        /// <summary>Invoked once WriteAllTextToTemp is about to return its temp path — lets a test
        /// simulate cancellation observed between staging and the subsequent rename.</summary>
        public Action? OnWriteAllTextToTemp { get; set; }

        public List<string> DeletedPaths { get; } = new();

        public bool FileExists(string path) => _existingPaths.Contains(path);

        public string ReadAllText(string path) => ReadContents;

        public void WriteAllText(string path, string contents)
        {
            LastWritePath = path;
            LastWriteContents = contents;
        }

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            LastWritePath = targetPath;
            LastWriteContents = contents;
            OnWriteAllTextToTemp?.Invoke();
            return targetPath + ".tmp";
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            RenameCount++;
        }

        public void DeleteFile(string path)
        {
            DeletedPaths.Add(path);
        }

        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public RecordingConsole()
        {
            Out = new StringWriter(_output);
            Error = new StringWriter(_error);
        }

        public TextWriter Out { get; }

        public TextWriter Error { get; }

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();
    }

    private sealed class StubRuntime : ICliRuntime
    {
        public PublicApiCaptureOutcome? CaptureOutcome { get; init; }

        public PublicApiDiffOutcome? DiffOutcome { get; init; }

        public PublicApiUpdateOutcome? UpdateOutcome { get; init; }

        public PublicApiMigrateOutcome? MigrateOutcome { get; init; }

        /// <summary>Thrown from the matching Public-API entrypoint instead of returning its
        /// outcome — used to simulate Core observing a cancelled token and raising
        /// OperationCanceledException.</summary>
        public Exception? CaptureException { get; init; }

        public Exception? DiffException { get; init; }

        public Exception? UpdateException { get; init; }

        public Exception? MigrateException { get; init; }

        /// <summary>Invoked once the matching entrypoint is about to return its outcome — lets a
        /// test simulate cancellation observed between Core returning and the handler's own
        /// subsequent two-phase publish step.</summary>
        public Action? OnCapturePublicApi { get; init; }

        public Action? OnUpdatePublicApi { get; init; }

        public Action? OnMigratePublicApi { get; init; }

        public string Version => "1.0.0";

        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request)
        {
            OnCapturePublicApi?.Invoke();
            return CaptureException != null ? throw CaptureException : CaptureOutcome ?? throw new NotSupportedException();
        }

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) =>
            DiffException != null ? throw DiffException : DiffOutcome ?? throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request)
        {
            OnUpdatePublicApi?.Invoke();
            return UpdateException != null ? throw UpdateException : UpdateOutcome ?? throw new NotSupportedException();
        }

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request)
        {
            OnMigratePublicApi?.Invoke();
            return MigrateException != null ? throw MigrateException : MigrateOutcome ?? throw new NotSupportedException();
        }

        // The delta formatter routes human output through the real Core formatter, so this stub
        // uses it too: the assertions above then exercise the same rendering the CLI ships.
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) =>
            new ArchitectureDiagnosticFormatter().FormatViolationsForHumans(violations);

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        // Also routed through the real Core formatters, mirroring CliRuntime, so `diff --format
        // json|sarif` exercises the same serialization the CLI actually ships instead of a stub.
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
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) =>
            ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
                mode, passed, violations, cycles, cycleFindings, classificationRoles,
                classificationPathDeferred, preflightDiagnostics, coverageFindings,
                unmatchedIgnoredViolations, policyConsistencyFindings, coverageSummaries,
                classificationConflicts, classificationMetadataFailures);

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) =>
            new ArchitectureSarifFormatter().FormatResultAsSarif(mode, violations, cycles, preflightDiagnostics, Version);

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

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
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            throw new NotSupportedException();

        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();

        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();

        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();

        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();

        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();

        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }
}
