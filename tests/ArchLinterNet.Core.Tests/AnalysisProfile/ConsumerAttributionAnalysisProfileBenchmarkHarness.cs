using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #461: consumer-shaped attribution evidence. This is an explicitly invoked, manual
// harness. It separates the CLI's inner profile from the outer process envelope while varying
// only the number and dispatch mode of independent processes over one unchanged build state.
[TestFixture]
[Explicit("Hardware-sensitive consumer attribution harness — run manually for issue #461 evidence.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class ConsumerAttributionAnalysisProfileBenchmarkHarness
{
    private const int RunsPerScenario = 10;
    private const int ProcessTimeoutMilliseconds = 300_000;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> _outputPhaseNames = new(StringComparer.Ordinal)
    {
        "render_human", "render_json", "render_sarif", "output_staging", "output_stream_write", "output_commit",
    };

    [Test]
    public async Task RunConsumerAttributionMatrix()
    {
        Assert.That(File.Exists(CliDllPath()), Is.True,
            $"CLI not built at {CliDllPath()} — run `dotnet build src/ArchLinterNet.Cli --no-restore` first.");

        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        Stopwatch buildClock = Stopwatch.StartNew();
        fixture.Build();
        buildClock.Stop();

        BatchSample priming = await RunBatchAsync(fixture, processCount: 1, dispatchMode: "sequential");
        ValidateSuccessfulBatch(priming, "warm-state priming");
        int projectsPerProcess = priming.Processes[0].Counters.DiscoveredProjectCount;

        List<ScenarioSummary> scenarios = new();
        ScenarioSeries single = await RunSeriesAsync(fixture, processCount: 1, dispatchMode: "sequential", prime: false);
        scenarios.Add(Summarize(
            "1-process-sequential", "One independent strict validation process", single));

        ScenarioSeries sequentialTwo = await RunSeriesAsync(fixture, processCount: 2, dispatchMode: "sequential", prime: false);
        scenarios.Add(Summarize(
            "2-process-sequential", "Two independent strict processes over one unchanged build", sequentialTwo));

        ScenarioSeries sequentialFour = await RunSeriesAsync(fixture, processCount: 4, dispatchMode: "sequential", prime: false);
        scenarios.Add(Summarize(
            "4-process-sequential", "Four independent strict processes over one unchanged build", sequentialFour));

        ScenarioSeries parallelTwo = await RunSeriesAsync(fixture, processCount: 2, dispatchMode: "bounded-parallel", prime: false);
        scenarios.Add(Summarize(
            "2-process-bounded-parallel", "Two independent strict processes dispatched together", parallelTwo));

        ScenarioSeries parallelFour = await RunSeriesAsync(fixture, processCount: 4, dispatchMode: "bounded-parallel", prime: false);
        scenarios.Add(Summarize(
            "4-process-bounded-parallel", "Four independent strict processes dispatched together", parallelFour));

        foreach (ScenarioSeries series in new[] { single, sequentialTwo, sequentialFour, parallelTwo, parallelFour })
        {
            foreach (BatchSample sample in series.MeasuredSamples)
            {
                ValidateSuccessfulBatch(sample, $"{sample.DispatchMode} x{sample.ProcessCount}");
                ValidatePerProcessCounters(sample, projectsPerProcess);
            }
        }

        BenchmarkEvidence evidence = new(
            "consumer-attribution-evidence/v1",
            "#461",
            EnvironmentIdentity.Create(),
            new FixtureIdentity(
                fixture.Id,
                fixture.ProjectPaths.Count,
                fixture.SourcePaths.Count,
                "Synthetic 8-host / 2-shared-library fixture; no private adopter data."),
            new BuildPreparation(buildClock.Elapsed.TotalMilliseconds, "One fixture build completed before timed validation batches."),
            scenarios,
            new AttributionDecision(
                "B",
                "ArchLinterNet inner work is measurable and is repeated once per independent process; no version regression is proven by this synthetic current-tree run.",
                "Outer process timing does not measure the caller's container or CI runner. Compare this artifact with consumer-side timestamps before asserting an external regression."),
            new RoutingDecision[]
            {
                new("#502", "Feed the reusable large-multi-host process-count shape and per-process profile counters into the canonical benchmark foundation; do not create a competing framework."),
                new("#492/#493", "Prepared-analysis reuse remains the owner for cross-process preparation; this harness measures duplication but does not implement reuse."),
                new("#655/#675/#503", "No selector/layer, real-MSBuild cache-eligibility, or changed-project advisory conclusion is asserted by this matrix."),
            },
            "The harness measures startup/process and ArchLinterNet profile boundaries only. Container, scheduler, restore-service, and CI-runner time require caller-side instrumentation.");

        File.WriteAllText(ResultsPath(), JsonSerializer.Serialize(evidence, _jsonOptions));
        TestContext.Out.WriteLine($"Consumer attribution evidence written to {ResultsPath()}");
        foreach (ScenarioSummary scenario in scenarios)
        {
            TestContext.Out.WriteLine(
                $"{scenario.ScenarioId} (n={scenario.SampleCount}): " +
                $"inner_median={scenario.MedianAggregateCommandTotalMs:F1}ms " +
                $"analysis_median={scenario.MedianAggregateAnalysisOnlyMs:F1}ms " +
                $"outer_median={scenario.MedianOuterWallClockMs:F1}ms");
        }
    }

    private static async Task<ScenarioSeries> RunSeriesAsync(
        AdoptionAcceptanceFixture fixture, int processCount, string dispatchMode, bool prime)
    {
        List<BatchSample> priming = new();
        if (prime)
        {
            BatchSample primeSample = await RunBatchAsync(fixture, processCount, dispatchMode);
            ValidateSuccessfulBatch(primeSample, $"priming {dispatchMode} x{processCount}");
            priming.Add(primeSample);
        }

        List<BatchSample> samples = new(RunsPerScenario);
        for (int index = 0; index < RunsPerScenario; index++)
        {
            samples.Add(await RunBatchAsync(fixture, processCount, dispatchMode));
        }

        return new ScenarioSeries(samples, priming);
    }

    private static async Task<BatchSample> RunBatchAsync(
        AdoptionAcceptanceFixture fixture, int processCount, string dispatchMode)
    {
        Stopwatch outerClock = Stopwatch.StartNew();
        IReadOnlyList<ProcessSample> processes;
        if (dispatchMode == "bounded-parallel")
        {
            Task<ProcessSample>[] tasks = Enumerable.Range(0, processCount)
                .Select(_ => RunProcessAsync(fixture))
                .ToArray();
            processes = await Task.WhenAll(tasks);
        }
        else
        {
            List<ProcessSample> samples = new(processCount);
            for (int index = 0; index < processCount; index++)
            {
                samples.Add(await RunProcessAsync(fixture));
            }

            processes = samples;
        }

        outerClock.Stop();
        Assert.That(processes, Has.Count.EqualTo(processCount));
        string digest = processes[0].CanonicalResultSha256;
        Assert.That(processes.Select(static process => process.CanonicalResultSha256).Distinct(),
            Is.EqualTo(new[] { digest }),
            $"Independent processes produced different canonical result projections in {dispatchMode} x{processCount}.");

        return new BatchSample(
            processCount,
            dispatchMode,
            outerClock.Elapsed.TotalMilliseconds,
            processes.Sum(static process => process.CommandTotalMs),
            processes.Sum(static process => process.PreflightMs),
            processes.Sum(static process => process.AnalysisOnlyMs),
            processes.Sum(static process => process.OutputMs),
            processes.Sum(static process => process.ProcessEnvelopeMs),
            CounterTotals.Sum(processes.Select(static process => process.Counters)),
            digest,
            processes);
    }

    private static async Task<ProcessSample> RunProcessAsync(AdoptionAcceptanceFixture fixture)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-461-{Guid.NewGuid():N}.json");
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = fixture.Root,
        };
        startInfo.ArgumentList.Add(CliDllPath());
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(fixture.PolicyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profilePath);
        startInfo.ArgumentList.Add("--ensure-built");
        startInfo.ArgumentList.Add("--max-parallelism");
        startInfo.ArgumentList.Add("1");

        Stopwatch wallClock = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)!;
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task exitTask = process.WaitForExitAsync();
        Task completed = await Task.WhenAny(exitTask, Task.Delay(ProcessTimeoutMilliseconds));
        if (completed != exitTask)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new AssertionException($"ArchLinterNet CLI process exceeded {ProcessTimeoutMilliseconds}ms.");
        }

        await exitTask;
        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        wallClock.Stop();

        Assert.That(File.Exists(profilePath), Is.True,
            $"No profile written (exit {process.ExitCode}).{Environment.NewLine}stdout:{stdout}{Environment.NewLine}stderr:{stderr}");
        try
        {
            using JsonDocument profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath));
            using JsonDocument resultDocument = JsonDocument.Parse(stdout);
            return CreateProcessSample(
                profileDocument.RootElement.Clone(),
                resultDocument.RootElement.GetRawText(),
                process.ExitCode,
                wallClock.Elapsed.TotalMilliseconds);
        }
        finally
        {
            File.Delete(profilePath);
        }
    }

    private static ProcessSample CreateProcessSample(
        JsonElement profile, string resultJson, int exitCode, double wallClockMs)
    {
        double preflightMs = 0;
        double outputMs = 0;
        double topLevelMs = 0;
        double? totalMs = null;
        foreach (JsonElement phase in profile.GetProperty("Phases").EnumerateArray())
        {
            string name = phase.GetProperty("Name").GetString()!;
            double elapsed = phase.GetProperty("ElapsedMs").GetDouble();
            if (name == "total")
            {
                totalMs = elapsed;
            }
            else if (phase.GetProperty("Indent").GetInt32() == 0)
            {
                topLevelMs += elapsed;
            }

            if (name is "build_state_preflight" or "post_ensure_built_preflight")
            {
                preflightMs += elapsed;
            }

            if (_outputPhaseNames.Contains(name))
            {
                outputMs += elapsed;
            }
        }

        double commandTotalMs = totalMs is { } total ? total + outputMs : topLevelMs;
        string canonicalResult = ExtractCanonicalResult(resultJson);
        return new ProcessSample(
            commandTotalMs,
            preflightMs,
            Math.Max(0, commandTotalMs - preflightMs - outputMs),
            outputMs,
            Math.Max(0, wallClockMs - commandTotalMs),
            profile.GetProperty("CompletionStatus").GetString()!,
            exitCode,
            profile.GetProperty("Output").GetProperty("OutputFailed").GetBoolean(),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalResult))),
            CounterTotals.From(profile),
            profile);
    }

    private static void ValidateSuccessfulBatch(BatchSample sample, string context)
    {
        Assert.Multiple(() =>
        {
            Assert.That(sample.Processes, Has.Count.EqualTo(sample.ProcessCount));
            Assert.That(sample.Processes.All(static process => process.CompletionStatus == "Success"), Is.True,
                $"Unexpected completion status in {context}.");
            Assert.That(sample.Processes.All(static process => process.ExitCode == 0), Is.True,
                $"Unexpected CLI exit category in {context}.");
            Assert.That(sample.Processes.All(static process => !process.OutputFailed), Is.True,
                $"Unexpected output publication failure in {context}.");
        });
    }

    private static void ValidatePerProcessCounters(BatchSample sample, int projectsPerProcess)
    {
        Assert.Multiple(() =>
        {
            Assert.That(sample.Processes.All(process => process.Counters.DiscoveredProjectCount == projectsPerProcess), Is.True,
                "Every independent process must retain the same discovered-project inventory.");
            Assert.That(sample.Counters.DiscoveredProjectCount, Is.EqualTo(projectsPerProcess * sample.ProcessCount),
                "Aggregate project preparation must show the duplicated work per process.");
            Assert.That(sample.Counters.ProjectGraphEvaluations, Is.GreaterThanOrEqualTo(sample.ProcessCount));
            Assert.That(sample.Counters.FactIndexMaterializations, Is.GreaterThanOrEqualTo(sample.ProcessCount));
            Assert.That(sample.Counters.SourceScanPasses, Is.GreaterThanOrEqualTo(sample.ProcessCount));
            Assert.That(sample.Counters.ContractExecutions, Is.GreaterThanOrEqualTo(sample.ProcessCount));
        });
    }

    private static ScenarioSummary Summarize(string id, string description, ScenarioSeries series)
    {
        return new ScenarioSummary(
            id,
            description,
            series.MeasuredSamples.Count,
            Median(series.MeasuredSamples.Select(static sample => sample.AggregateCommandTotalMs)),
            Percentile95(series.MeasuredSamples.Select(static sample => sample.AggregateCommandTotalMs)),
            Median(series.MeasuredSamples.Select(static sample => sample.AggregateAnalysisOnlyMs)),
            Percentile95(series.MeasuredSamples.Select(static sample => sample.AggregateAnalysisOnlyMs)),
            Median(series.MeasuredSamples.Select(static sample => sample.OuterWallClockMs)),
            Percentile95(series.MeasuredSamples.Select(static sample => sample.OuterWallClockMs)),
            Median(series.MeasuredSamples.Select(static sample => sample.AggregateProcessEnvelopeMs)),
            series.MeasuredSamples,
            series.PrimingSamples);
    }

    private static string ExtractCanonicalResult(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string[] canonicalFields =
        [
            "passed", "violations", "cycles", "cycle_findings", "coverage_findings",
            "unmatched_ignored_violations", "policy_consistency_findings", "classification_conflicts",
            "classification_metadata_failures",
        ];
        return string.Join("\n", canonicalFields.Select(field =>
            root.TryGetProperty(field, out JsonElement value) ? value.GetRawText() : "null"));
    }

    private static double Median(IEnumerable<double> values)
    {
        List<double> sorted = values.Order().ToList();
        int midpoint = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[midpoint - 1] + sorted[midpoint]) / 2 : sorted[midpoint];
    }

    private static double Percentile95(IEnumerable<double> values)
    {
        List<double> sorted = values.Order().ToList();
        int index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static string CliDllPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    private static string ResultsPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "consumer-attribution-analysis-profile-results.json");

    private sealed record ProcessSample(
        double CommandTotalMs,
        double PreflightMs,
        double AnalysisOnlyMs,
        double OutputMs,
        double ProcessEnvelopeMs,
        string CompletionStatus,
        int ExitCode,
        bool OutputFailed,
        string CanonicalResultSha256,
        CounterTotals Counters,
        JsonElement Profile);

    private sealed record BatchSample(
        int ProcessCount,
        string DispatchMode,
        double OuterWallClockMs,
        double AggregateCommandTotalMs,
        double AggregatePreflightMs,
        double AggregateAnalysisOnlyMs,
        double AggregateOutputMs,
        double AggregateProcessEnvelopeMs,
        CounterTotals Counters,
        string CanonicalResultSha256,
        IReadOnlyList<ProcessSample> Processes);

    private sealed record ScenarioSeries(
        IReadOnlyList<BatchSample> MeasuredSamples,
        IReadOnlyList<BatchSample> PrimingSamples);

    private sealed record ScenarioSummary(
        string ScenarioId,
        string Description,
        int SampleCount,
        double MedianAggregateCommandTotalMs,
        double P95AggregateCommandTotalMs,
        double MedianAggregateAnalysisOnlyMs,
        double P95AggregateAnalysisOnlyMs,
        double MedianOuterWallClockMs,
        double P95OuterWallClockMs,
        double MedianAggregateProcessEnvelopeMs,
        IReadOnlyList<BatchSample> Samples,
        IReadOnlyList<BatchSample> PrimingSamples);

    private sealed record CounterTotals(
        int PolicyCompositions,
        int ProjectGraphEvaluations,
        int AssemblyLoads,
        int DiscoveredProjectCount,
        int RetainedAssemblyCount,
        int SelectedAssemblyCount,
        int ModesEvaluated,
        int SnapshotMaterializations,
        int FactIndexMaterializations,
        int SourceScanPasses,
        int SourceFilesScanned,
        int ContractExecutions,
        int ContractResults)
    {
        public static CounterTotals From(JsonElement profile)
        {
            JsonElement counters = profile.GetProperty("Counters");
            return new CounterTotals(
                counters.GetProperty("PolicyCompositions").GetInt32(),
                counters.GetProperty("ProjectGraphEvaluations").GetInt32(),
                counters.GetProperty("AssemblyLoads").GetInt32(),
                counters.GetProperty("DiscoveredProjectCount").GetInt32(),
                counters.GetProperty("RetainedAssemblyCount").GetInt32(),
                counters.GetProperty("SelectedAssemblyCount").GetInt32(),
                counters.GetProperty("ModesEvaluated").GetInt32(),
                counters.GetProperty("SnapshotMaterializations").GetInt32(),
                counters.GetProperty("FactIndexMaterializations").GetInt32(),
                counters.GetProperty("SourceScanPasses").GetInt32(),
                counters.GetProperty("SourceFilesScanned").GetInt32(),
                SumObject(counters.GetProperty("ContractFamilyCounts")),
                SumObject(counters.GetProperty("ContractFamilyResultCounts")));
        }

        public static CounterTotals Sum(IEnumerable<CounterTotals> counters) => counters.Aggregate(
            new CounterTotals(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Add);

        private static CounterTotals Add(CounterTotals left, CounterTotals right) => new(
            left.PolicyCompositions + right.PolicyCompositions,
            left.ProjectGraphEvaluations + right.ProjectGraphEvaluations,
            left.AssemblyLoads + right.AssemblyLoads,
            left.DiscoveredProjectCount + right.DiscoveredProjectCount,
            left.RetainedAssemblyCount + right.RetainedAssemblyCount,
            left.SelectedAssemblyCount + right.SelectedAssemblyCount,
            left.ModesEvaluated + right.ModesEvaluated,
            left.SnapshotMaterializations + right.SnapshotMaterializations,
            left.FactIndexMaterializations + right.FactIndexMaterializations,
            left.SourceScanPasses + right.SourceScanPasses,
            left.SourceFilesScanned + right.SourceFilesScanned,
            left.ContractExecutions + right.ContractExecutions,
            left.ContractResults + right.ContractResults);

        private static int SumObject(JsonElement value) => value.EnumerateObject().Sum(property => property.Value.GetInt32());
    }

    private sealed record BenchmarkEvidence(
        string SchemaId,
        string Issue,
        EnvironmentIdentity Environment,
        FixtureIdentity Fixture,
        BuildPreparation BuildPreparation,
        IReadOnlyList<ScenarioSummary> Scenarios,
        AttributionDecision Attribution,
        IReadOnlyList<RoutingDecision> Routing,
        string MeasurementBoundary);

    private sealed record EnvironmentIdentity(
        string OperatingSystem,
        string Runtime,
        string Architecture,
        int ProcessorCount,
        string SourceCommit,
        string Configuration)
    {
        public static EnvironmentIdentity Create() => new(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown",
            "Debug");
    }

    private sealed record FixtureIdentity(string Id, int ProjectCount, int SourceFileCount, string Description);

    private sealed record BuildPreparation(double BuildWallClockMs, string Description);

    private sealed record AttributionDecision(string State, string Conclusion, string Limitation);

    private sealed record RoutingDecision(string Owner, string Decision);
}
