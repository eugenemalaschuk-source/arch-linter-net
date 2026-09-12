using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.EntryPoint;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Commands.Cache;
using ArchLinterNet.Cli.Commands.Change.EntryPoint;
using ArchLinterNet.Cli.Commands.Coverage.EntryPoint;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Gate.EntryPoint;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Commands.Health.EntryPoint;
using ArchLinterNet.Cli.Commands.Measure.EntryPoint;
using ArchLinterNet.Cli.Commands.Policy;
using ArchLinterNet.Cli.Commands.PublicApi;
using ArchLinterNet.Cli.Commands.Schema;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Commands.Validate.EntryPoint;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

internal abstract class CliArchitectureTestBase
{
    protected static readonly string[] _value = { "badge", "baseline", "cache", "change", "coverage", "graph", "explain", "gate", "health", "history", "measure", "policy", "public-api", "report", "scaffold", "schema", "topology" };
    protected static readonly string[] _value1 = { "generate", "update", "prune", "diff", "verify", "migrate" };
    protected static readonly string[] _value2 = { "rule-1" };
    protected sealed class FakeCliRuntime : ICliRuntime
    {
        public string Version => "1.2.3";

        public ValidationRequest? LastValidationRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public ValidationOutcome? ForcedOutcome { get; init; }

        public ArchitectureMetricMeasurementOutcome? ForcedMeasurementOutcome { get; init; }

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

        public ArchitectureMetricMeasurementOutcome Measure(
            ArchitectureMetricMeasurementRequest request,
            ValidationTiming? timing) =>
            ForcedMeasurementOutcome ?? throw ExceptionToThrow ?? new NotSupportedException();

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
            throw new NotSupportedException();
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

    protected sealed class FakeCliConsole : ICliConsole
    {
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();

        public TextWriter Out => new StringWriter(_stdout);

        public TextWriter Error => new StringWriter(_stderr);

        public string StdOut => _stdout.ToString();

        public string StdErr => _stderr.ToString();
    }

    protected sealed class FakeFileSystem(bool exists) : IFileSystem
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

        public bool CanWriteToDirectory(string path) => true;
    }
}
