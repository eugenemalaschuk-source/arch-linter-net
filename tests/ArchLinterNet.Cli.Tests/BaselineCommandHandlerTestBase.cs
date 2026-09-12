using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

internal abstract class BaselineCommandHandlerTestBase
{
    protected static readonly string[] ContractIds = ["rule-a", "rule-b"];

    protected static readonly BaselineReasonOptions Reasons =
        new("reason", Array.Empty<string>(), Array.Empty<string>());

    protected static readonly BaselineWriteOptions WriteOptions = new(DryRun: false, Force: false);

    protected static ArchitectureViolation CreateViolation(string sourceType, string forbiddenNamespace)
    {
        return new ArchitectureViolation("contract", "rule", sourceType, forbiddenNamespace, ["ref"]);
    }

    /// <summary>
    /// The lifecycle report Core would attach, so counts in these handler tests come from the same
    /// single source the real outcomes use.
    /// </summary>
    protected static IReadOnlyList<BaselineLifecycleEntry> BuildLifecycleReport(
        ArchitectureBaselineComparisonEntry newEntry,
        ArchitectureBaselineComparisonEntry matchedEntry,
        ArchitectureBaselineComparisonEntry resolvedEntry,
        ArchitectureBaselineComparisonEntry unknownContractEntry)
    {
        return
        [
            new BaselineLifecycleEntry(newEntry, BaselineEntryLifecycle.New),
            new BaselineLifecycleEntry(matchedEntry, BaselineEntryLifecycle.Matched),
            new BaselineLifecycleEntry(resolvedEntry, BaselineEntryLifecycle.Resolved),
            new BaselineLifecycleEntry(unknownContractEntry, BaselineEntryLifecycle.Stale),
        ];
    }

    protected static ArchitectureBaselineComparisonEntry CreateEntry(
        string contractGroup,
        string contractId,
        string sourceType,
        string forbiddenReference,
        string reason)
    {
        return new ArchitectureBaselineComparisonEntry(contractGroup, contractId, sourceType, forbiddenReference, reason);
    }

    protected static void AssertGuardCase(
        Func<RecordingConsole, int> execute,
        string expectedError,
        bool expectsJson = false)
    {
        var console = new RecordingConsole();
        int result = execute(console);
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        if (expectsJson)
        {
            Assert.That(console.ErrorText, Is.Empty);
            Assert.That(console.OutputText, Is.Not.Empty);
            using JsonDocument document = JsonDocument.Parse(console.OutputText);
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("error"));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain(expectedError));
            return;
        }

        Assert.That(console.OutputText, Is.Empty);
        Assert.That(console.ErrorText, Does.Contain(expectedError));
    }

    protected sealed class StubFileSystem(params string[] existingPaths) : IFileSystem
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);

        public string? LastWritePath { get; private set; }

        public string? LastWriteContents { get; private set; }

        public bool FileExists(string path) => _existingPaths.Contains(path);

        public string ReadAllText(string path) => string.Empty;

        public void WriteAllText(string path, string contents)
        {
            LastWritePath = path;
            LastWriteContents = contents;
        }

        /// <summary>Set to simulate a write failure and prove the destination is left alone.</summary>
        public Exception? TempWriteException { get; set; }

        public int RenameCount { get; private set; }

        /// <summary>Invoked once WriteAllTextToTemp is about to return its temp path — lets a test
        /// simulate cancellation observed between staging and the subsequent rename.</summary>
        public Action? OnWriteAllTextToTemp { get; set; }

        public List<string> DeletedPaths { get; } = new();

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            if (TempWriteException != null)
            {
                throw TempWriteException;
            }

            LastWritePath = targetPath;
            LastWriteContents = contents;
            OnWriteAllTextToTemp?.Invoke();
            return targetPath + ".tmp";
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            RenameCount++;
        }

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => !FileExists(targetPath);

        public void DeleteFile(string path)
        {
            DeletedPaths.Add(path);
        }

        public bool TryCreateNewFile(string path) => true;

        public bool DirectoryExists(string path) => true;

        public void DeleteDirectoryIfEmpty(string path) { }

        public bool CanWriteToDirectory(string path) => true;
    }

    protected sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        private readonly TextWriter _out;
        private readonly TextWriter _errorWriter;

        public RecordingConsole()
        {
            _out = new StringWriter(_output);
            _errorWriter = new StringWriter(_error);
        }

        public TextWriter Out => _out;

        public TextWriter Error => _errorWriter;

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();
    }

    protected sealed class StubRuntime : ICliRuntime
    {
        private static readonly ArchitectureDependencyGraph _emptyGraph =
            new(Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>());

        public string Version => "1.0.0";

        public BaselineGenerationOutcome GenerateOutcome { get; set; } =
            new(true, "generated", 0, Array.Empty<ArchitectureViolation>());

        public BaselineUpdateOutcome UpdateOutcome { get; set; } =
            new(true, "updated", 0, 0, Array.Empty<ArchitectureViolation>());

        public BaselinePruneOutcome PruneOutcome { get; set; } =
            new(true, "pruned", Array.Empty<BaselineRemovedEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineDiffOutcome DiffOutcome { get; set; } =
            new(true, Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineVerifyOutcome VerifyOutcome { get; set; } =
            new(true, true, Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineMigrateOutcome MigrateOutcome { get; set; } =
            new(true, "migrated", 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>());

        public Exception? GenerateException { get; set; }

        public Exception? UpdateException { get; set; }

        /// <summary>Invoked once UpdateBaseline is about to return its outcome — lets a test
        /// simulate cancellation observed between Core returning and the handler's own
        /// subsequent write/publish step.</summary>
        public Action? OnUpdateBaseline { get; set; }

        public Action? OnGenerateBaseline { get; set; }

        public Exception? PruneException { get; set; }

        public Exception? DiffException { get; set; }

        public Exception? VerifyException { get; set; }

        public Exception? MigrateException { get; set; }

        public BaselineGenerationRequest? GenerateRequest { get; private set; }

        public BaselineUpdateRequest? UpdateRequest { get; private set; }

        public BaselinePruneRequest? PruneRequest { get; private set; }

        public BaselineDiffRequest? DiffRequest { get; private set; }

        public BaselineVerifyRequest? VerifyRequest { get; private set; }

        public BaselineMigrateRequest? MigrateRequest { get; private set; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => Enum.TryParse(value, true, out level);

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();

        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();
        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => throw new NotSupportedException();

        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => throw new NotSupportedException();

        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => throw new NotSupportedException();

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => throw new NotSupportedException();

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => throw new NotSupportedException();

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => throw new NotSupportedException();

        public string FormatClassificationFactsForHumans(IReadOnlyCollection<ArchitectureClassificationConflict> conflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();

        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request)
        {
            GenerateRequest = request;
            OnGenerateBaseline?.Invoke();
            return GenerateException == null ? GenerateOutcome : throw GenerateException;
        }

        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request)
        {
            UpdateRequest = request;
            OnUpdateBaseline?.Invoke();
            return UpdateException == null ? UpdateOutcome : throw UpdateException;
        }

        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request)
        {
            PruneRequest = request;
            return PruneException == null ? PruneOutcome : throw PruneException;
        }

        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
        {
            DiffRequest = request;
            return DiffException == null ? DiffOutcome : throw DiffException;
        }

        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request)
        {
            VerifyRequest = request;
            return VerifyException == null ? VerifyOutcome : throw VerifyException;
        }

        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request)
        {
            MigrateRequest = request;
            return MigrateException == null ? MigrateOutcome : throw MigrateException;
        }

        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => new(_emptyGraph);

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => "digraph G {}";

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => "graph TD";

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => new("Source", "Target", null, Array.Empty<string>());
    }
}
