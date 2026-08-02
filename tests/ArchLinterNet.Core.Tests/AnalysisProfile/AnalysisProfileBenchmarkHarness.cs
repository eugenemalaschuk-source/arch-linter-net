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
// Results are written to a JSON file under the OS temp directory (path printed to the NUnit
// output). Each sample retains its complete raw analysis profile so processor time, allocations,
// memory, publication evidence, and deterministic counters remain available for #409. Copy that
// file into docs/internal/analysis-profile-pre-optimization-baseline-results.json, then update
// docs/internal/analysis-profile-pre-optimization-baseline.md from the same run; never fabricate
// the numbers there.
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

    private static string CliDllPath()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
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
        List<RunSample> coldSeries = RunColdSeries(RunsPerScenario);
        summaries.Add(Summarize(
            "1-cold-process-warm-filesystem-strict", "First --ensure-built run on a never-built fixture copy",
            coldSeries, expectedStatus: "Success"));

        using AdoptionAcceptanceFixture warmFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        List<RunSample> warmSeries = RunSeries(
            warmFixture.Root, warmFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeStatus: "Success");
        summaries.Add(Summarize(
            "2-immediate-warm-strict-repeat", "Same fixture, repeat --ensure-built runs (no persistent cache exists yet — #365)",
            warmSeries, expectedStatus: "Success"));

        // Scenario 3: strict and audit as two separate legacy-style processes.
        using AdoptionAcceptanceFixture legacyFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        List<RunSample> legacyStrict = RunSeries(
            legacyFixture.Root, legacyFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeStatus: "Success");
        List<RunSample> legacyAudit = RunSeries(
            legacyFixture.Root, legacyFixture.PolicyPath, mode: "audit", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: false);
        Assert.That(legacyStrict.Concat(legacyAudit).All(sample => sample.CompletionStatus == "Success"), Is.True,
            "Unexpected failed/cancelled sample in the strict/audit comparison");
        List<double> legacyPairedTotals = legacyStrict.Zip(legacyAudit, (s, a) => s.AnalysisMs + a.AnalysisMs).ToList();
        summaries.Add(new ScenarioSummary(
            "3-strict-and-audit-separate-processes",
            "Sum of one strict process + one audit process (10 paired runs)",
            legacyPairedTotals.Count, Median(legacyPairedTotals), Percentile95(legacyPairedTotals),
            Median(legacyStrict.Select(r => r.PreflightMs).ToList()), "Success/Success",
            legacyStrict.Concat(legacyAudit).ToList()));

        // Scenario 4: combined strict+audit from one #363 snapshot.
        using AdoptionAcceptanceFixture combinedFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        List<RunSample> combinedSeries = RunSeries(
            combinedFixture.Root, combinedFixture.PolicyPath, mode: "strict,audit", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeStatus: "Success");
        summaries.Add(Summarize(
            "4-combined-strict-audit-one-snapshot", "One process, --mode strict,audit (one #363 snapshot)", combinedSeries,
            expectedStatus: "Success"));

        // Scenario 5: one report sink versus human+JSON+SARIF through #364.
        using AdoptionAcceptanceFixture sinkFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        List<RunSample> oneSink = RunSeries(
            sinkFixture.Root, sinkFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: ["--report", "json=stdout"], count: RunsPerScenario, prime: true,
            expectedPrimeStatus: "Success");
        // --report only allows one sink per destination (stdout can carry one format at a time —
        // see ValidateCommandDefinition.ParseReportSinks), so the three-sink comparison spreads
        // across stdout + two files, matching the existing Checkpoint A three-sink scenario
        // (ValidateCommandHandlerCheckpointATests.CheckpointA_HumanJsonAndSarifSinks_ExecuteOneAnalysis).
        List<RunSample> threeSinks = RunSeries(
            sinkFixture.Root, sinkFixture.PolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: ["--report", "human=stdout", "--report", "json=result.json", "--report", "sarif=result.sarif"],
            count: RunsPerScenario, prime: false);
        summaries.Add(Summarize("5a-one-report-sink", "--report json=stdout", oneSink, expectedStatus: "Success"));
        summaries.Add(Summarize(
            "5b-three-report-sinks", "--report human=stdout json=result.json sarif=result.sarif (one analysis, three renders)", threeSinks,
            expectedStatus: "Success"));

        // Scenario 6: sequential execution before #408 — this whole matrix already runs
        // sequentially (no parallelism exists yet); no separate timed variant is meaningful.

        // Scenario 7: representative success/validation-failure/preparation-failure paths.
        // Success is already demonstrated by scenarios 1/2/3/4/5 above.
        using AdoptionAcceptanceFixture successFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        string failingPolicyPath = Path.Combine(successFixture.Root, "dependencies.failing.arch.yml");
        List<RunSample> validationFailureSeries = RunSeries(
            successFixture.Root, failingPolicyPath, mode: "strict", ensureBuilt: true,
            extraArgs: null, count: RunsPerScenario, prime: true, expectedPrimeStatus: "ValidationFailure");
        summaries.Add(Summarize(
            "7b-validation-failure-completion-path", "Intentionally-failing policy variant", validationFailureSeries,
            expectedStatus: "ValidationFailure"));

        using AdoptionAcceptanceFixture unbuiltFixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        List<RunSample> preparationFailureSeries = RunSeries(
            unbuiltFixture.Root, unbuiltFixture.PolicyPath, mode: "strict", ensureBuilt: false,
            extraArgs: ["--no-restore"], count: RunsPerScenario, prime: false);
        summaries.Add(Summarize(
            "7c-preparation-failure-completion-path", "Never-built fixture, --no-restore, no receipts", preparationFailureSeries,
            expectedStatus: "PreparationFailure"));

        string resultsPath = Path.Combine(Path.GetTempPath(), "analysis-profile-benchmark-results.json");
        File.WriteAllText(resultsPath, JsonSerializer.Serialize(summaries, _jsonOptions));
        TestContext.Out.WriteLine($"Benchmark results written to {resultsPath}");
        foreach (ScenarioSummary summary in summaries)
        {
            TestContext.Out.WriteLine(
                $"{summary.ScenarioId} (n={summary.SampleCount}): median={summary.MedianAnalysisMs:F1}ms " +
                $"p95={summary.P95AnalysisMs:F1}ms preflight_median={summary.MedianPreflightMs:F1}ms " +
                $"status={summary.CompletionStatus} — {summary.Description}");
        }
    }

    private static ScenarioSummary Summarize(
        string id, string description, List<RunSample> samples, string expectedStatus)
    {
        List<RunSample> includedSamples = samples
            .Where(sample => sample.CompletionStatus == expectedStatus)
            .ToList();
        Assert.That(includedSamples, Has.Count.EqualTo(samples.Count),
            $"Unexpected failed/cancelled sample in {id}; expected {expectedStatus}");

        List<double> analysisMs = includedSamples.Select(s => s.AnalysisMs).ToList();
        return new ScenarioSummary(
            id, description, includedSamples.Count, Median(analysisMs), Percentile95(analysisMs),
            Median(includedSamples.Select(s => s.PreflightMs).ToList()), expectedStatus, includedSamples);
    }

    private static List<RunSample> RunSeries(
        string fixtureRoot, string policyPath, string mode, bool ensureBuilt, IReadOnlyList<string>? extraArgs,
        int count, bool prime, string? expectedPrimeStatus = null)
    {
        if (prime)
        {
            RunSample primeSample = RunOnce(fixtureRoot, policyPath, mode, ensureBuilt, extraArgs);
            Assert.That(expectedPrimeStatus, Is.Not.Null,
                "Every requested priming run must declare the completion status it is expected to produce.");
            Assert.That(primeSample.CompletionStatus, Is.EqualTo(expectedPrimeStatus),
                $"Priming run failed for policy '{policyPath}' mode '{mode}'. The measured warm samples are invalid.");
        }

        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            samples.Add(RunOnce(fixtureRoot, policyPath, mode, ensureBuilt, extraArgs));
        }

        return samples;
    }

    private static List<RunSample> RunColdSeries(int count)
    {
        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("large-multi-host");
            samples.Add(RunOnce(fixture.Root, fixture.PolicyPath, "strict", ensureBuilt: true, extraArgs: null));
        }

        return samples;
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

        // Only the single-mode Validate() path wraps its work in a "total" Measure() call — the
        // combined --mode strict,audit path calls CreateSnapshot() directly, which has no such
        // wrapper (see ArchitectureValidationApplicationService). When "total" is absent, sum the
        // top-level (Indent 0) phases instead — safe because they never overlap each other, unlike
        // "total" itself, which if present already encompasses every other Indent-0 phase and must
        // not also be added to their sum.
        double? explicitTotalMs = null;
        double indentZeroSumMs = 0;
        double preflightMs = 0;
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
        }

        double totalMs = explicitTotalMs ?? indentZeroSumMs;

        return new RunSample(
            totalMs, preflightMs, Math.Max(0, totalMs - preflightMs), completionStatus, wallClock.Elapsed.TotalMilliseconds,
            profile);
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
        double TotalMs,
        double PreflightMs,
        double AnalysisMs,
        string CompletionStatus,
        double WallClockMs,
        JsonElement Profile);

    private sealed record ScenarioSummary(
        string ScenarioId, string Description, int SampleCount, double MedianAnalysisMs, double P95AnalysisMs,
        double MedianPreflightMs, string CompletionStatus, IReadOnlyList<RunSample> Samples);
}
