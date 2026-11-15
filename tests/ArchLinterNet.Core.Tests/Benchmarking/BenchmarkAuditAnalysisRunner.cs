using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Tests;

internal sealed record BenchmarkAuditAnalysisRun(
    BenchmarkProfileSample Sample,
    BenchmarkCanonicalResultIdentity CanonicalResult,
    ValidationOutcome Outcome,
    IReadOnlyDictionary<string, int> TimedContractFamilyCounts,
    string MaterializedInputSha256);

internal static class BenchmarkAuditAnalysisRunner
{
    public static BenchmarkAuditAnalysisRun Run(
        BenchmarkMaterializedFixture fixture,
        bool observerEnabled,
        int sampleOrdinal,
        bool isWarmSample,
        string parallelMode,
        int? maxParallelism,
        BenchmarkResourceMeasurement fixtureMaterialization,
        BenchmarkResourceMeasurement restoreAndBuild)
    {
        ValidationTiming timing = ValidationTiming.CreateForAnalysisProfile();
        BenchmarkWorkObserver observer = new();
        ArchitecturePolicyDocumentLoader loader = new();
        var setupService = new ArchitectureRunnerSetupService(
            loader,
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            new ArchitectureProjectDiscoveryService(),
            new ArchitectureAssemblyResolutionService());
        ArchitectureAnalysisSnapshot? snapshot = null;
        Stopwatch elapsed = Stopwatch.StartNew();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        TimeSpan processorTimeBefore = Process.GetCurrentProcess().TotalProcessorTime;
        try
        {
            ArchitectureContractDocument document = loader.Load(fixture.PolicyPath);
            ArchitectureRunnerSetup setup = observerEnabled
                ? setupService.BuildRunnerWithBenchmarkObserver(
                    document,
                    fixture.PolicyPath,
                    observer,
                    timing: timing,
                    mode: ArchitectureRunnerSetupService.PreparedCandidateResolutionMode,
                    maxParallelism: maxParallelism)
                : setupService.BuildRunner(
                    document,
                    fixture.PolicyPath,
                    timing: timing,
                    mode: ArchitectureRunnerSetupService.PreparedCandidateResolutionMode,
                    maxParallelism: maxParallelism);
            var registry = new ArchitectureContractHandlerRegistry();
            snapshot = new ArchitectureAnalysisSnapshot(
                document,
                setup,
                new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>()),
                document.Analysis.UnmatchedIgnoredViolations,
                document.Analysis.PolicyConsistency,
                document.Analysis.Coverage,
                enforceUnmatchedIgnoredViolationsPolicy: false,
                includeAsmdefContracts: true,
                new ArchitectureContractExecutor(),
                registry,
                policyCompositions: 1,
                projectGraphEvaluations: document.Analysis.Projects.Count > 0 ? 1 : 0,
                assemblyLoads: setup.AssemblyLoads);

            ValidationOutcome outcome = snapshot.Evaluate("strict", timing);
            elapsed.Stop();
            long allocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore);
            TimeSpan processorTime = Process.GetCurrentProcess().TotalProcessorTime - processorTimeBefore;
            BenchmarkCanonicalResultIdentity canonical = CreateCanonicalResult(outcome);
            AnalysisProfile profile = AnalysisProfileBuilder.Build(
                snapshot.Counters,
                timing,
                renderedSinkCount: 0,
                outputSinkCount: 0,
                outcome.Passed ? AnalysisProfileCompletionStatus.Success : AnalysisProfileCompletionStatus.ValidationFailure,
                cancellationObserved: false);
            using JsonDocument profileDocument = JsonDocument.Parse(AnalysisProfileJsonWriter.Write(profile));
            string inputIdentity = BenchmarkMaterializedInputIdentity.Compute(fixture.Root);
            var sample = new BenchmarkProfileSample
            {
                Run = new BenchmarkRunDescriptor
                {
                    ExecutionMode = fixture.Definition.Workflow.ExecutionMode,
                    CacheMode = "disabled",
                    PreparedStateMode = "unprepared",
                    ParallelMode = parallelMode,
                    SampleOrdinal = sampleOrdinal,
                    IsWarmSample = isWarmSample,
                },
                RawAnalysisProfile = profileDocument.RootElement.Clone(),
                CompletionStatus = outcome.Passed ? "Success" : "ValidationFailure",
                ExitCode = outcome.Passed ? 0 : 1,
                OutputFailed = false,
                WallClock = BenchmarkResourceMeasurement.Available(
                    Math.Max(1, (long)Math.Ceiling(elapsed.Elapsed.TotalMilliseconds)), "milliseconds"),
                MeasuredWallClockMilliseconds = (decimal)elapsed.Elapsed.TotalMilliseconds,
                ProcessorTime = BenchmarkResourceMeasurement.Available(
                    Math.Max(0, (long)Math.Ceiling(processorTime.TotalMilliseconds)), "milliseconds"),
                AllocatedBytes = BenchmarkResourceMeasurement.Available(allocatedBytes, "bytes"),
                PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable(
                    "Per-invocation peak retained managed memory is not sampled by this in-process harness."),
                WorkCounters = observerEnabled
                    ? BenchmarkWorkCounterEvidence.FromObserver(observer)
                    : BenchmarkWorkCounterEvidence.ObserverDisabled("The per-invocation Core work observer was disabled for this paired sample."),
                HistoryWorkCounters = BenchmarkHistoryWorkCounterEvidence.NotApplicableToCoreWorkload(),
                TimingBoundaries = new BenchmarkTimingBoundaryEvidence
                {
                    Contract = BenchmarkTimingBoundaryEvidence.BoundaryContract,
                    FixtureMaterialization = fixtureMaterialization,
                    RestoreAndBuild = restoreAndBuild,
                    ProcessHostStartupAndShutdown = BenchmarkResourceMeasurement.NotApplicable(
                        "This direct Core harness runs inside the test host; no analyzer child process is started."),
                    CliProcess = BenchmarkResourceMeasurement.NotApplicable(
                        "The measured path is the Core runner and does not launch the CLI."),
                    CoreAnalysisSession = BenchmarkResourceMeasurement.Available(
                        Math.Max(1, (long)Math.Ceiling(elapsed.Elapsed.TotalMilliseconds)), "milliseconds"),
                    ResultPublication = BenchmarkResourceMeasurement.NotApplicable(
                        "The direct Core harness returns a ValidationOutcome and does not publish a CLI report."),
                    TotalEndToEnd = BenchmarkResourceMeasurement.Unavailable(
                        "Fixture setup is shared across paired observer samples; only this isolated Core interval is attributed per sample."),
                },
            };
            return new BenchmarkAuditAnalysisRun(
                sample,
                canonical,
                outcome,
                timing.Entries.Where(entry => entry.Count.HasValue)
                    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Count!.Value), StringComparer.Ordinal),
                inputIdentity);
        }
        finally
        {
            elapsed.Stop();
            snapshot?.Dispose();
        }
    }

    public static BenchmarkProfilerOverheadEvidence CreateObserverOverhead(
        BenchmarkAuditAnalysisRun observerDisabled,
        BenchmarkAuditAnalysisRun observerEnabled)
    {
        return new BenchmarkProfilerOverheadEvidence
        {
            Instrumentation = BenchmarkProfilerOverheadEvidence.InstrumentationId,
            ObserverDisabledAnalysis = ToMilliseconds(observerDisabled.Sample),
            ObserverEnabledAnalysis = ToMilliseconds(observerEnabled.Sample),
            ObserverDisabledAllocatedBytes = observerDisabled.Sample.AllocatedBytes,
            ObserverEnabledAllocatedBytes = observerEnabled.Sample.AllocatedBytes,
            ObserverDisabledPeakRetainedBytes = BenchmarkResourceMeasurement.Unavailable(
                "Peak retained memory was not sampled for the observer-disabled run."),
            ObserverEnabledPeakRetainedBytes = BenchmarkResourceMeasurement.Unavailable(
                "Peak retained memory was not sampled for the observer-enabled run."),
            CanonicalResultsMatch = string.Equals(
                observerDisabled.CanonicalResult.Sha256,
                observerEnabled.CanonicalResult.Sha256,
                StringComparison.Ordinal),
        };
    }

    private static BenchmarkResourceMeasurement ToMilliseconds(BenchmarkProfileSample sample) =>
        BenchmarkResourceMeasurement.Available(
            Math.Max(1, (long)Math.Ceiling(sample.MeasuredWallClockMilliseconds ?? 0)), "milliseconds");

    private static BenchmarkCanonicalResultIdentity CreateCanonicalResult(ValidationOutcome outcome)
    {
        List<BenchmarkFindingIdentity> findings = [];
        findings.AddRange(outcome.Violations.Select(violation => new BenchmarkFindingIdentity
        {
            ContractId = violation.ContractId ?? string.Empty,
            Kind = $"{violation.ContractName}|{violation.ForbiddenNamespace}",
            SourceAssembly = string.Empty,
            SourceType = violation.SourceType,
            SourceMember = string.Join(";", violation.ForbiddenReferences),
            Location = string.Empty,
        }));
        findings.AddRange(outcome.Cycles.Select(cycle => new BenchmarkFindingIdentity
        {
            ContractId = string.Empty,
            Kind = "cycle",
            SourceAssembly = string.Empty,
            SourceType = string.Empty,
            SourceMember = cycle,
            Location = string.Empty,
        }));
        findings.AddRange(outcome.CycleFindings.Select(finding => new BenchmarkFindingIdentity
        {
            ContractId = finding.ContractId ?? string.Empty,
            Kind = $"cycle-finding|{finding.ContractName}",
            SourceAssembly = string.Empty,
            SourceType = string.Empty,
            SourceMember = finding.Path,
            Location = string.Empty,
        }));
        findings.AddRange(outcome.CoverageFindings.Select(violation => new BenchmarkFindingIdentity
        {
            ContractId = violation.ContractId ?? string.Empty,
            Kind = $"coverage|{violation.ContractName}|{violation.ForbiddenNamespace}",
            SourceAssembly = string.Empty,
            SourceType = violation.SourceType,
            SourceMember = string.Join(";", violation.ForbiddenReferences),
            Location = string.Empty,
        }));
        findings.AddRange(outcome.UnmatchedIgnoredViolations.Select(violation => new BenchmarkFindingIdentity
        {
            ContractId = violation.ContractId ?? string.Empty,
            Kind = $"unmatched-ignore|{violation.ContractName}",
            SourceAssembly = string.Empty,
            SourceType = violation.SourceType,
            SourceMember = $"{violation.ForbiddenReference}|{violation.Reason}|{violation.IgnoreIndex}",
            Location = string.Empty,
        }));
        return BenchmarkIdentity.CreateCanonicalResult(
            outcome.Passed ? "Success" : "ValidationFailure",
            outcome.Passed ? 0 : 1,
            findings);
    }
}

internal static class BenchmarkMaterializedInputIdentity
{
    public static string Compute(string root)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                     .Where(IsAnalyzedInput)
                     .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(relativePath));
            hash.AppendData([0]);
            using FileStream stream = File.OpenRead(path);
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }

            hash.AppendData([0]);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static bool IsAnalyzedInput(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension is not (".cs" or ".csproj" or ".dll" or ".json" or ".slnx" or ".yml"))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        return !normalized.Contains("/obj/", StringComparison.Ordinal)
            && !normalized.Contains("/analysis-cache/", StringComparison.Ordinal)
            && !Path.GetFileName(path).StartsWith("profile-", StringComparison.Ordinal)
            && !Path.GetFileName(path).StartsWith("output-", StringComparison.Ordinal);
    }
}
