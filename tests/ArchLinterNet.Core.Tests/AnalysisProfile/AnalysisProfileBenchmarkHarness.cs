using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #374: the repeatable pre-optimization benchmark harness. NOT run by `make test`/`make
// acceptance` — see openspec/specs/analysis-profile/spec.md, "A repeatable benchmark harness
// produces checked-in pre-optimization evidence": correctness tests gate deterministic counters
// and invariants, never hardware-specific duration limits. Run manually:
//
//   rtk dotnet test tests/ArchLinterNet.Core.Tests --filter FullyQualifiedName~AnalysisProfileBenchmarkHarness
//
// Results are written directly to docs/internal/analysis-profile-pre-optimization-baseline-results.json
// so the complete raw profile from every sample is committed alongside the human-readable baseline.
// This preserves processor time, allocations, memory, publication evidence, and deterministic
// counters for #409; update the Markdown document from the same run and never fabricate its numbers.
[TestFixture]
[Explicit("Hardware-sensitive benchmark harness — run manually to refresh pre-optimization evidence, never in CI.")]
[Category("Benchmark")]
// 95 real CLI subprocess invocations across 7 scenarios legitimately take several minutes —
// see PerTestDurationGuardAttribute's own suggested remedy for a test that needs more than its
// default 15s budget.
[CancelAfter(600_000)]
public sealed class AnalysisProfileBenchmarkHarness
{
    private const int RunsPerScenario = 10;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> _outputPhaseNames = new(StringComparer.Ordinal)
    {
        "render_human",
        "render_json",
        "render_sarif",
        "output_staging",
        "output_stream_write",
        "output_commit",
    };

    private static readonly ExpectedSampleOutcome _successfulOutcome = new("Success", 0, OutputFailed: false);
    private static readonly ExpectedSampleOutcome _validationFailureOutcome = new("ValidationFailure", 1, OutputFailed: false);
    private static readonly ExpectedSampleOutcome _preparationFailureOutcome = new("PreparationFailure", 1, OutputFailed: false);

    private static string CliDllPath()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
    }

    private static string ResultsPath()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(repositoryRoot, "docs", "internal", "analysis-profile-pre-optimization-baseline-results.json");
    }

    [Test]
    public void RunBenchmarkMatrix()
    {
        Assert.That(File.Exists(CliDllPath()), Is.True,
            $"CLI not built at {CliDllPath()} — run `rtk dotnet build` first.");

        List<ScenarioSummary> summaries = new();

        // Every cold sample gets a fresh, never-built fixture. A single first run cannot provide
        // a meaningful p95, and splitting one shared series into 1 cold + 9 warm samples violates
        // the required ten-run-per-scenario evidence contract.
        SampleSeries coldSeries = RunColdSeries(RunsPerScenario);
        summaries.Add(Summarize(
            "1-cold-process-warm-filesystem-strict", "First --ensure-built run on a never-built fixture copy",
            coldSeries, _successfulOutcome));

        using AdoptionAcceptanceFixture warmFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        SampleSeries warmSeries = RunSeries(
            warmFixture.Root, warmFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeOutcome: _successfulOutcome);
        summaries.Add(Summarize(
            "2-immediate-warm-strict-repeat", "Same fixture, repeat --ensure-built runs (no persistent cache exists yet — #365)",
            warmSeries, _successfulOutcome));

        // Scenario 3: strict and audit as two separate legacy-style processes.
        using AdoptionAcceptanceFixture legacyFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        SampleSeries legacyStrict = RunSeries(
            legacyFixture.Root, legacyFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeOutcome: _successfulOutcome);
        SampleSeries legacyAudit = RunSeries(
            legacyFixture.Root, legacyFixture.PolicyPath, mode: "audit", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: false);
        ValidateSamples(legacyStrict.MeasuredSamples, _successfulOutcome, "3-strict-and-audit-separate-processes strict");
        ValidateSamples(legacyAudit.MeasuredSamples, _successfulOutcome, "3-strict-and-audit-separate-processes audit");
        List<double> legacyPairedAnalysisOnly = legacyStrict.MeasuredSamples
            .Zip(legacyAudit.MeasuredSamples, (s, a) => s.AnalysisOnlyMs + a.AnalysisOnlyMs).ToList();
        List<double> legacyPairedOutput = legacyStrict.MeasuredSamples
            .Zip(legacyAudit.MeasuredSamples, (s, a) => s.OutputMs + a.OutputMs).ToList();
        List<double> legacyPairedCommandTotal = legacyStrict.MeasuredSamples
            .Zip(legacyAudit.MeasuredSamples, (s, a) => s.CommandTotalMs + a.CommandTotalMs).ToList();
        List<double> legacyPairedPreflight = legacyStrict.MeasuredSamples
            .Zip(legacyAudit.MeasuredSamples, (s, a) => s.PreflightMs + a.PreflightMs).ToList();
        summaries.Add(new ScenarioSummary(
            "3-strict-and-audit-separate-processes",
            "Sum of one strict process + one audit process (10 paired runs)",
            legacyPairedAnalysisOnly.Count,
            Median(legacyPairedAnalysisOnly), Percentile95(legacyPairedAnalysisOnly),
            Median(legacyPairedOutput), Percentile95(legacyPairedOutput),
            Median(legacyPairedCommandTotal), Percentile95(legacyPairedCommandTotal),
            Median(legacyPairedPreflight), "Success/Success",
            legacyStrict.MeasuredSamples.Concat(legacyAudit.MeasuredSamples).ToList(),
            legacyStrict.PrimingSamples.Concat(legacyAudit.PrimingSamples).ToList()));

        // Scenario 4: combined strict+audit from one #363 snapshot.
        using AdoptionAcceptanceFixture combinedFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        SampleSeries combinedSeries = RunSeries(
            combinedFixture.Root, combinedFixture.PolicyPath, mode: "strict,audit", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeOutcome: _successfulOutcome);
        summaries.Add(Summarize(
            "4-combined-strict-audit-one-snapshot", "One process, --mode strict,audit (one #363 snapshot)", combinedSeries,
            _successfulOutcome));

        // Scenario 5: one report sink versus human+JSON+SARIF through #364.
        using AdoptionAcceptanceFixture sinkFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        SampleSeries oneSink = RunSeries(
            sinkFixture.Root, sinkFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: ["--report", "json=stdout"], count: RunsPerScenario, prime: true,
            expectedPrimeOutcome: _successfulOutcome);
        // --report only allows one sink per destination (stdout can carry one format at a time —
        // see ValidateCommandDefinition.ParseReportSinks), so the three-sink comparison spreads
        // across stdout + two files, matching the existing Checkpoint A three-sink scenario
        // (ValidateCommandHandlerCheckpointATests.CheckpointA_HumanJsonAndSarifSinks_ExecuteOneAnalysis).
        SampleSeries threeSinks = RunSeries(
            sinkFixture.Root, sinkFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: ["--report", "human=stdout", "--report", "json=result.json", "--report", "sarif=result.sarif"],
            count: RunsPerScenario, prime: false);
        summaries.Add(Summarize("5a-one-report-sink", "--report json=stdout", oneSink, _successfulOutcome));
        summaries.Add(Summarize(
            "5b-three-report-sinks", "--report human=stdout json=result.json sarif=result.sarif (one analysis, three renders)", threeSinks,
            _successfulOutcome));

        // Scenario 6: sequential execution before #408 — this whole matrix already runs
        // sequentially (no parallelism exists yet); no separate timed variant is meaningful.

        // Scenario 7: representative success/validation-failure/preparation-failure paths.
        // Success is already demonstrated by scenarios 1/2/3/4/5 above.
        using AdoptionAcceptanceFixture successFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        string failingPolicyPath = Path.Combine(successFixture.Root, "dependencies.failing.arch.yml");
        SampleSeries validationFailureSeries = RunSeries(
            successFixture.Root, failingPolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeOutcome: _validationFailureOutcome);
        summaries.Add(Summarize(
            "7b-validation-failure-completion-path", "Intentionally-failing policy variant", validationFailureSeries,
            _validationFailureOutcome));

        using AdoptionAcceptanceFixture unbuiltFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        SampleSeries preparationFailureSeries = RunSeries(
            unbuiltFixture.Root, unbuiltFixture.PolicyPath, mode: "strict", ensureBuilt: false,
            extraArgs: ["--no-restore"], count: RunsPerScenario, prime: false);
        summaries.Add(Summarize(
            "7c-preparation-failure-completion-path", "Never-built fixture, --no-restore, no receipts", preparationFailureSeries,
            _preparationFailureOutcome));

        string resultsPath = ResultsPath();
        File.WriteAllText(resultsPath, JsonSerializer.Serialize(summaries, _jsonOptions));
        TestContext.Out.WriteLine($"Benchmark results written to {resultsPath}");
        foreach (ScenarioSummary summary in summaries)
        {
            TestContext.Out.WriteLine(
                $"{summary.ScenarioId} (n={summary.SampleCount}): analysis_median={summary.MedianAnalysisOnlyMs:F1}ms " +
                $"output_median={summary.MedianOutputMs:F1}ms command_median={summary.MedianCommandTotalMs:F1}ms " +
                $"preflight_median={summary.MedianPreflightMs:F1}ms " +
                $"status={summary.CompletionStatus} — {summary.Description}");
        }
    }

    private static ScenarioSummary Summarize(
        string id, string description, SampleSeries series, ExpectedSampleOutcome expectedOutcome)
    {
        IReadOnlyList<RunSample> samples = series.MeasuredSamples;
        ValidateSamples(samples, expectedOutcome, id);
        return new ScenarioSummary(
            id, description, samples.Count,
            Median(samples.Select(s => s.AnalysisOnlyMs).ToList()), Percentile95(samples.Select(s => s.AnalysisOnlyMs).ToList()),
            Median(samples.Select(s => s.OutputMs).ToList()), Percentile95(samples.Select(s => s.OutputMs).ToList()),
            Median(samples.Select(s => s.CommandTotalMs).ToList()), Percentile95(samples.Select(s => s.CommandTotalMs).ToList()),
            Median(samples.Select(s => s.PreflightMs).ToList()), expectedOutcome.CompletionStatus,
            samples, series.PrimingSamples);
    }

    private static void ValidateSamples(
        IReadOnlyList<RunSample> samples, ExpectedSampleOutcome expectedOutcome, string scenario)
    {
        for (int i = 0; i < samples.Count; i++)
        {
            ValidateSample(samples[i], expectedOutcome, $"{scenario} sample {i + 1}");
        }
    }

    private static void ValidateSample(RunSample sample, ExpectedSampleOutcome expectedOutcome, string context)
    {
        string observed = $"completion={sample.CompletionStatus}, exit={sample.ExitCode}, output_failed={sample.OutputFailed}";
        Assert.That(sample.CompletionStatus, Is.EqualTo(expectedOutcome.CompletionStatus),
            $"Unexpected analysis completion for {context}: {observed}");
        Assert.That(sample.ExitCode, Is.EqualTo(expectedOutcome.ExitCode),
            $"Unexpected CLI exit category for {context}: {observed}");
        Assert.That(sample.OutputFailed, Is.EqualTo(expectedOutcome.OutputFailed),
            $"Unexpected report-publication outcome for {context}: {observed}");
    }

    private static SampleSeries RunSeries(
        string fixtureRoot, string policyPath, string mode, bool ensureBuilt, IReadOnlyList<string>? extraArgs,
        int count, bool prime, ExpectedSampleOutcome? expectedPrimeOutcome = null)
    {
        List<RunSample> primingSamples = new();
        if (prime)
        {
            RunSample primeSample = RunOnce(fixtureRoot, policyPath, mode, ensureBuilt, extraArgs);
            Assert.That(expectedPrimeOutcome, Is.Not.Null,
                "Every requested priming run must declare its expected completion, exit category, and output outcome.");
            ValidateSample(
                primeSample, expectedPrimeOutcome!,
                $"priming run for policy '{policyPath}' mode '{mode}'; measured warm samples are invalid");
            primingSamples.Add(primeSample);
        }

        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            samples.Add(RunOnce(fixtureRoot, policyPath, mode, ensureBuilt, extraArgs));
        }

        return new SampleSeries(samples, primingSamples);
    }

    private static SampleSeries RunColdSeries(int count)
    {
        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("large-multi-host");
            samples.Add(RunOnce(fixture.Root, fixture.PolicyPath, "strict", ensureBuilt: true, extraArgs: null));
        }

        return new SampleSeries(samples, Array.Empty<RunSample>());
    }

    private static RunSample RunOnce(
        string fixtureRoot, string policyPath, string mode, bool ensureBuilt, IReadOnlyList<string>? extraArgs)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = fixtureRoot,
        };
        startInfo.ArgumentList.Add(CliDllPath());
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profilePath);
        if (ensureBuilt)
        {
            startInfo.ArgumentList.Add("--ensure-built");
        }
        foreach (string arg in extraArgs ?? [])
        {
            startInfo.ArgumentList.Add(arg);
        }

        Stopwatch wallClock = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)!;
        // Drain both redirected pipes concurrently. Reading stdout to completion before stderr
        // can deadlock a child that fills the stderr pipe while ensure-built is reporting its
        // build/restore diagnostics; a stuck sample would make the entire matrix hang before it
        // can write the checked-in baseline evidence.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        wallClock.Stop();

        Assert.That(File.Exists(profilePath), Is.True,
            $"No profile written for policy '{policyPath}' mode '{mode}'.{Environment.NewLine}stdout:{stdout}{Environment.NewLine}stderr:{stderr}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(profilePath));
        JsonElement profile = document.RootElement.Clone();
        File.Delete(profilePath);
        JsonElement root = profile;
        string completionStatus = root.GetProperty("CompletionStatus").GetString()!;
        bool outputFailed = root.GetProperty("Output").GetProperty("OutputFailed").GetBoolean();

        // Only the single-mode Validate() path wraps its work in a "total" Measure() call — the
        // combined --mode strict,audit path calls CreateSnapshot() directly, which has no such
        // wrapper (see ArchitectureValidationApplicationService). `total` ends before normal
        // report routing, whereas a combined top-level phase sum includes it. Keep the comparable
        // boundaries explicit: AnalysisOnly excludes preflight and every rendering/publication
        // phase; Output is the latter phase set; CommandTotal includes all three kinds of work.
        double? explicitTotalMs = null;
        double indentZeroSumMs = 0;
        double preflightMs = 0;
        double outputMs = 0;
        foreach (JsonElement phase in root.GetProperty("Phases").EnumerateArray())
        {
            string name = phase.GetProperty("Name").GetString()!;
            int indent = phase.GetProperty("Indent").GetInt32();
            double elapsed = phase.GetProperty("ElapsedMs").GetDouble();
            if (name == "total")
            {
                explicitTotalMs = elapsed;
            }
            else if (indent == 0)
            {
                indentZeroSumMs += elapsed;
            }

            if (name == "build_state_preflight")
            {
                preflightMs += elapsed;
            }

            if (_outputPhaseNames.Contains(name))
            {
                outputMs += elapsed;
            }
        }

        double commandTotalMs = explicitTotalMs is { } analysisAndPreflightMs
            ? analysisAndPreflightMs + outputMs
            : indentZeroSumMs;
        double analysisOnlyMs = Math.Max(0, commandTotalMs - preflightMs - outputMs);

        return new RunSample(
            commandTotalMs, preflightMs, analysisOnlyMs, outputMs, completionStatus, process.ExitCode, outputFailed,
            wallClock.Elapsed.TotalMilliseconds, profile);
    }

    private static double Median(List<double> values)
    {
        List<double> sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }

    private static double Percentile95(List<double> values)
    {
        List<double> sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    // Preserve the complete profile rather than only its elapsed fields. This is the raw baseline
    // evidence for #409, including per-phase ProcessorTimeMs, Measurements, Output, and every
    // deterministic counter for each individual process.
    private sealed record RunSample(
        double CommandTotalMs,
        double PreflightMs,
        double AnalysisOnlyMs,
        double OutputMs,
        string CompletionStatus,
        int ExitCode,
        bool OutputFailed,
        double WallClockMs,
        JsonElement Profile);

    private sealed record ScenarioSummary(
        string ScenarioId,
        string Description,
        int SampleCount,
        double MedianAnalysisOnlyMs,
        double P95AnalysisOnlyMs,
        double MedianOutputMs,
        double P95OutputMs,
        double MedianCommandTotalMs,
        double P95CommandTotalMs,
        double MedianPreflightMs,
        string CompletionStatus,
        IReadOnlyList<RunSample> Samples,
        IReadOnlyList<RunSample> PrimingSamples);

    private sealed record ExpectedSampleOutcome(string CompletionStatus, int ExitCode, bool OutputFailed);

    private sealed record SampleSeries(
        IReadOnlyList<RunSample> MeasuredSamples,
        IReadOnlyList<RunSample> PrimingSamples);
}
