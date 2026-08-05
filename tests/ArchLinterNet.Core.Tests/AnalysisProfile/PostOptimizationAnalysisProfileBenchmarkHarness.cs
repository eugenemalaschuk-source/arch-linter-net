using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #409: manual, hardware-sensitive release evidence. This deliberately remains separate
// from #374's historical pre-optimization harness and is never part of normal acceptance.
[TestFixture]
[Explicit("Hardware-sensitive post-cache/post-parallel benchmark — run manually for release evidence only.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class PostOptimizationAnalysisProfileBenchmarkHarness
{
    private const int RunsPerScenario = 10;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> _outputPhaseNames = new(StringComparer.Ordinal)
    {
        "render_human", "render_json", "render_sarif", "output_staging", "output_stream_write", "output_commit",
    };

    [Test]
    public void RunPostOptimizationMatrix()
    {
        Assert.That(File.Exists(CliDllPath()), Is.True,
            $"CLI not built at {CliDllPath()} — run `dotnet build src/ArchLinterNet.Cli --no-restore` first.");

        List<ScenarioSummary> scenarios = new();
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("large-multi-host");
        fixture.Build();
        // The measured ordinary/cache runs must not hide build recovery work. Establish the
        // receipt-backed build state once, outside the timing dataset, then exercise the
        // metadata-only preparation path against those exact artifacts.
        ValidateSuccess(RunOnce(fixture, "strict", ["--ensure-built", "--max-parallelism", "1"]), "fixture build-state priming");

        SampleSeries disabled = RunSeries(fixture, "strict", ["--ensure-built", "--max-parallelism", "1"], RunsPerScenario, prime: true);
        scenarios.Add(Summarize("2-immediate-warm-strict-repeat-cache-disabled", "Warm strict repeat with cache disabled", disabled));

        using TemporaryDirectory cache = TemporaryDirectory.Create("arch-linter-net-409-cache");
        SampleSeries population = RunSeries(
            fixture, "strict", ["--ensure-built", "--cache", cache.Path, "--max-parallelism", "1"], RunsPerScenario, prime: false,
            freshCachePerSample: true);
        scenarios.Add(Summarize("6a-cache-first-population", "First cache population with an isolated cache root", population));

        SampleSeries warmHit = RunWarmHitSeries(fixture, cache.Path, RunsPerScenario);
        scenarios.Add(Summarize("6b-cache-verified-warm-hit", "Verified warm cache hit after population", warmHit));
        AssertEquivalentSamples(disabled.MeasuredSamples, warmHit.MeasuredSamples, "cached and uncached canonical findings");
        Assert.That(warmHit.MeasuredSamples.All(static sample =>
                CacheCounter(sample, "Hits") == 1
                && Counter(sample, "AssemblyLoads") == 0
                && CacheCounter(sample, "AvoidedAssemblyLoads") > 0
                && (CacheCounter(sample, "AvoidedFactIndexMaterializations") > 0
                    || CacheCounter(sample, "AvoidedContractExecutions") > 0)
                && !sample.Profile.GetProperty("Phases").EnumerateArray()
                    .Any(phase => phase.GetProperty("Name").GetString() == "contract_checks")), Is.True,
            "Every warm hit must avoid CLR loading and fact/contract work, without contract_checks.");

        SampleSeries sequential = RunSeries(fixture, "strict", ["--ensure-built", "--max-parallelism", "1"], RunsPerScenario, prime: false);
        SampleSeries parallel = RunSeries(fixture, "strict", ["--ensure-built"], RunsPerScenario, prime: false);
        scenarios.Add(Summarize("7a-sequential-max-parallelism-1", "Fully supported sequential execution", sequential));
        scenarios.Add(Summarize("7b-bounded-parallel-default", "Documented default bounded parallel assembly/fact scanning", parallel));
        AssertEquivalentSamples(sequential.MeasuredSamples, parallel.MeasuredSamples, "sequential and parallel canonical findings");
        Assert.That(parallel.MeasuredSamples.All(static sample =>
                ConcurrencyStatus(sample) == "Active"
                && ConcurrencyCounter(sample, "MaxParallelism") > 1
                && ConcurrencyCounter(sample, "ScheduledWorkItems") >= 4
                && ConcurrencyCounter(sample, "CompletedWorkItems") == ConcurrencyCounter(sample, "ScheduledWorkItems")
                && ConcurrencyCounter(sample, "ObservedMaxConcurrency") >= 2
                && ConcurrencyCounter(sample, "MergeOperations") > 0
                && Counter(sample, "FactIndexMaterializations") > 0), Is.True,
            "Parallel evidence must activate bounded fact work, complete every partition, and merge deterministically.");

        SampleSeries legacyStrict = RunSeries(fixture, "strict", ["--ensure-built", "--max-parallelism", "1"], RunsPerScenario, prime: false);
        SampleSeries legacyAudit = RunSeries(fixture, "audit", ["--ensure-built", "--max-parallelism", "1"], RunsPerScenario, prime: false);
        scenarios.Add(Pair("3-strict-and-audit-separate-processes", "Paired strict and audit processes", legacyStrict, legacyAudit));

        SampleSeries combined = RunSeries(fixture, "strict,audit", ["--ensure-built", "--max-parallelism", "1"], RunsPerScenario, prime: false);
        scenarios.Add(Summarize("4-combined-strict-audit-one-snapshot", "Combined strict and audit from one snapshot", combined));
        Assert.That(combined.MeasuredSamples.All(static sample =>
            Counter(sample, "PolicyCompositions") == 1 && Counter(sample, "ProjectGraphEvaluations") == 2), Is.True,
            "Combined execution must compose policy once; --ensure-built records initial evaluation plus its required post-build preparation.");

        SampleSeries oneSink = RunSeries(fixture, "strict", ["--ensure-built", "--report", "json=one.json", "--max-parallelism", "1"], RunsPerScenario, prime: false);
        SampleSeries threeSinks = RunSeries(fixture, "strict", [
            "--ensure-built", "--report", "human=human.txt", "--report", "json=result.json", "--report", "sarif=result.sarif", "--max-parallelism", "1",
        ], RunsPerScenario, prime: false);
        scenarios.Add(Summarize("5a-one-report-sink", "One JSON report sink", oneSink));
        scenarios.Add(Summarize("5b-three-report-sinks", "Human, JSON, and SARIF report sinks", threeSinks));
        AssertOutputOnlyDifference(oneSink.MeasuredSamples[0], threeSinks.MeasuredSamples[0]);

        IReadOnlyList<RunSample> unsuccessful = RunUnsuccessfulEvidence(fixture);

        BenchmarkEvidence evidence = new(
            EnvironmentIdentity.Create(), scenarios, unsuccessful,
            "Successful timing summaries exclude cancellation, validation, preparation, and output failures.");
        File.WriteAllText(ResultsPath(), JsonSerializer.Serialize(evidence, _jsonOptions));
        TestContext.Out.WriteLine($"Post-optimization evidence written to {ResultsPath()}");
    }

    private static SampleSeries RunWarmHitSeries(AdoptionAcceptanceFixture fixture, string cachePath, int count)
    {
        RunSample population = RunOnce(fixture, "strict", ["--ensure-built", "--cache", cachePath, "--max-parallelism", "1"]);
        ValidateSuccess(population, "cache population");
        Assert.That(CacheCounter(population, "Writes"), Is.GreaterThan(0), "Population must write a cache entry.");

        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            RunSample sample = RunOnce(fixture, "strict", ["--ensure-built", "--cache", cachePath, "--max-parallelism", "1"]);
            ValidateSuccess(sample, $"warm-hit sample {i + 1}");
            Assert.That(CacheCounter(sample, "Hits"), Is.GreaterThan(0), "Warm run must be a cache hit.");
            samples.Add(sample);
        }

        return new SampleSeries(samples, [population]);
    }

    private static SampleSeries RunSeries(
        AdoptionAcceptanceFixture fixture, string mode, IReadOnlyList<string> arguments, int count, bool prime,
        bool freshCachePerSample = false)
    {
        List<RunSample> priming = new();
        if (prime)
        {
            RunSample sample = RunOnce(fixture, mode, arguments);
            ValidateSuccess(sample, $"priming {mode}");
            priming.Add(sample);
        }

        List<RunSample> samples = new(count);
        for (int i = 0; i < count; i++)
        {
            if (freshCachePerSample)
            {
                using TemporaryDirectory isolatedCache = TemporaryDirectory.Create("arch-linter-net-409-populate");
                IReadOnlyList<string> isolatedArguments = ReplaceCachePath(arguments, isolatedCache.Path);
                RunSample sample = RunOnce(fixture, mode, isolatedArguments);
                ValidateSuccess(sample, $"population sample {i + 1}");
                Assert.That(CacheCounter(sample, "Writes"), Is.GreaterThan(0),
                    $"Population must write a cache entry. Profile cache: {sample.Profile.GetProperty("Counters").GetProperty("Cache").GetRawText()}");
                samples.Add(sample);
            }
            else
            {
                RunSample sample = RunOnce(fixture, mode, arguments);
                ValidateSuccess(sample, $"{mode} sample {i + 1}");
                samples.Add(sample);
            }
        }

        return new SampleSeries(samples, priming);
    }

    private static IReadOnlyList<string> ReplaceCachePath(IReadOnlyList<string> arguments, string cachePath)
    {
        List<string> replaced = arguments.ToList();
        int index = replaced.IndexOf("--cache");
        if (index >= 0)
        {
            replaced[index + 1] = cachePath;
        }

        return replaced;
    }

    private static IReadOnlyList<RunSample> RunUnsuccessfulEvidence(AdoptionAcceptanceFixture fixture)
    {
        string failingPolicy = Path.Combine(fixture.Root, "dependencies.failing.arch.yml");
        RunSample validationFailure = RunOnce(fixture, "strict", ["--ensure-built", "--max-parallelism", "1"], failingPolicy);
        Assert.That(validationFailure.CompletionStatus, Is.EqualTo("ValidationFailure"));
        Assert.That(validationFailure.ExitCode, Is.EqualTo(1));

        using AdoptionAcceptanceFixture unbuilt = AdoptionAcceptanceFixture.Create("large-multi-host");
        RunSample preparationFailure = RunOnce(unbuilt, "strict", ["--no-restore", "--max-parallelism", "1"]);
        Assert.That(preparationFailure.CompletionStatus, Is.EqualTo("PreparationFailure"));
        Assert.That(preparationFailure.ExitCode, Is.EqualTo(1));
        return [validationFailure, preparationFailure];
    }

    private static void AssertEquivalent(RunSample left, RunSample right, string comparison)
    {
        Assert.That(right.CanonicalResult, Is.EqualTo(left.CanonicalResult), $"Mismatch in {comparison}.");
    }

    private static void AssertEquivalentSamples(
        IReadOnlyList<RunSample> baseline, IReadOnlyList<RunSample> candidate, string comparison)
    {
        Assert.That(candidate.Count, Is.EqualTo(baseline.Count), $"Sample-count mismatch in {comparison}.");
        for (int index = 0; index < baseline.Count; index++)
        {
            RunSample left = baseline[index];
            RunSample right = candidate[index];
            Assert.Multiple(() =>
            {
                AssertEquivalent(left, right, $"{comparison} sample {index + 1}");
                Assert.That(right.CompletionStatus, Is.EqualTo(left.CompletionStatus));
                Assert.That(right.ExitCode, Is.EqualTo(left.ExitCode));
                Assert.That(right.OutputFailed, Is.EqualTo(left.OutputFailed));
            });
        }
    }

    private static void AssertOutputOnlyDifference(RunSample oneSink, RunSample threeSinks)
    {
        AssertEquivalent(oneSink, threeSinks, "one- and three-sink canonical findings");
        Assert.That(Counter(threeSinks, "RenderedSinkCount"), Is.GreaterThan(Counter(oneSink, "RenderedSinkCount")));
        Assert.That(Counter(threeSinks, "OutputSinkCount"), Is.GreaterThan(Counter(oneSink, "OutputSinkCount")));
        Assert.That(Counter(threeSinks, "PolicyCompositions"), Is.EqualTo(Counter(oneSink, "PolicyCompositions")));
        Assert.That(Counter(threeSinks, "ProjectGraphEvaluations"), Is.EqualTo(Counter(oneSink, "ProjectGraphEvaluations")));
        Assert.That(Counter(threeSinks, "AssemblyLoads"), Is.EqualTo(Counter(oneSink, "AssemblyLoads")));
    }

    private static ScenarioSummary Summarize(string id, string description, SampleSeries series)
    {
        IReadOnlyList<RunSample> samples = series.MeasuredSamples;
        return new ScenarioSummary(
            id, description, samples.Count,
            Median(samples.Select(static sample => sample.AnalysisOnlyMs)), Percentile95(samples.Select(static sample => sample.AnalysisOnlyMs)),
            Median(samples.Select(static sample => sample.OutputMs)), Percentile95(samples.Select(static sample => sample.OutputMs)),
            Median(samples.Select(static sample => sample.CommandTotalMs)), Percentile95(samples.Select(static sample => sample.CommandTotalMs)),
            Median(samples.Select(static sample => sample.PreflightMs)),
            Median(samples.Select(static sample => sample.WallClockMs)), Percentile95(samples.Select(static sample => sample.WallClockMs)),
            MedianOptional(samples.Select(AllocatedBytes)), Percentile95Optional(samples.Select(AllocatedBytes)), samples, series.PrimingSamples);
    }

    private static ScenarioSummary Pair(string id, string description, SampleSeries strict, SampleSeries audit)
    {
        IReadOnlyList<RunSample> samples = strict.MeasuredSamples.Zip(audit.MeasuredSamples, Combine).ToList();
        return Summarize(id, description, new SampleSeries(samples, strict.PrimingSamples.Concat(audit.PrimingSamples).ToList()));
    }

    private static RunSample Combine(RunSample strict, RunSample audit) => new(
        strict.CommandTotalMs + audit.CommandTotalMs, strict.PreflightMs + audit.PreflightMs,
        strict.AnalysisOnlyMs + audit.AnalysisOnlyMs, strict.OutputMs + audit.OutputMs,
        "Success/Success", 0, false, strict.WallClockMs + audit.WallClockMs,
        strict.CanonicalResult + audit.CanonicalResult, strict.RawResult + audit.RawResult,
        strict.Profile, [strict.Profile, audit.Profile]);

    private static RunSample RunOnce(
        AdoptionAcceptanceFixture fixture, string mode, IReadOnlyList<string> arguments, string? policyPath = null)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-409-profile-{Guid.NewGuid():N}.json");
        ProcessStartInfo startInfo = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = fixture.Root };
        startInfo.ArgumentList.Add(CliDllPath());
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policyPath ?? fixture.PolicyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode);
        string? jsonReportPath = FindJsonReportPath(fixture.Root, arguments);
        if (jsonReportPath is null)
        {
            startInfo.ArgumentList.Add("--format");
            startInfo.ArgumentList.Add("json");
        }
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profilePath);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Stopwatch wallClock = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)!;
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        wallClock.Stop();

        Assert.That(File.Exists(profilePath), Is.True,
            $"No profile produced (exit {process.ExitCode}). stdout:{Environment.NewLine}{stdout}{Environment.NewLine}stderr:{Environment.NewLine}{stderr}");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(profilePath));
            JsonElement profile = document.RootElement.Clone();
            string resultJson = jsonReportPath is null ? stdout : File.ReadAllText(jsonReportPath);
            return CreateRunSample(profile, ExtractCanonicalResult(resultJson), resultJson, process.ExitCode, wallClock.Elapsed.TotalMilliseconds);
        }
        finally
        {
            File.Delete(profilePath);
        }
    }

    private static RunSample CreateRunSample(
        JsonElement profile, string canonicalResult, string rawResult, int exitCode, double wallClockMs)
    {
        JsonElement root = profile;
        double preflightMs = 0;
        double outputMs = 0;
        double topLevelMs = 0;
        double? totalMs = null;
        foreach (JsonElement phase in root.GetProperty("Phases").EnumerateArray())
        {
            string name = phase.GetProperty("Name").GetString()!;
            double elapsed = phase.GetProperty("ElapsedMs").GetDouble();
            if (name == "total") totalMs = elapsed;
            else if (phase.GetProperty("Indent").GetInt32() == 0) topLevelMs += elapsed;
            if (name is "build_state_preflight" or "post_ensure_built_preflight") preflightMs += elapsed;
            if (_outputPhaseNames.Contains(name)) outputMs += elapsed;
        }

        double commandTotalMs = totalMs is { } total ? total + outputMs : topLevelMs;
        return new RunSample(commandTotalMs, preflightMs, Math.Max(0, commandTotalMs - preflightMs - outputMs), outputMs,
            root.GetProperty("CompletionStatus").GetString()!, exitCode,
            root.GetProperty("Output").GetProperty("OutputFailed").GetBoolean(), wallClockMs, canonicalResult, rawResult, profile);
    }

    private static void ValidateSuccess(RunSample sample, string context)
    {
        Assert.That(sample.CompletionStatus, Is.EqualTo("Success"),
            $"Unexpected completion for {context}. preflight:{Environment.NewLine}{PreflightDiagnostics(sample.RawResult)}");
        Assert.That(sample.ExitCode, Is.EqualTo(0), $"Unexpected exit category for {context}.");
        Assert.That(sample.OutputFailed, Is.False, $"Unexpected output failure for {context}.");
    }

    private static string ExtractCanonicalResult(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string[] canonicalFields = [
            "passed", "violations", "cycles", "cycle_findings", "coverage_findings",
            "unmatched_ignored_violations", "policy_consistency_findings", "classification_conflicts",
            "classification_metadata_failures",
        ];
        return string.Join("\n", canonicalFields.Select(field =>
            root.TryGetProperty(field, out JsonElement value) ? value.GetRawText() : "null"));
    }

    private static string PreflightDiagnostics(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("preflight_diagnostics", out JsonElement diagnostics))
        {
            return json;
        }

        return string.Join(Environment.NewLine, diagnostics.EnumerateArray().Select(diagnostic => string.Join(", ",
            diagnostic.EnumerateObject().Where(property => property.Name is "state" or "project_path" or "assembly_name" or "detail" or "cache_eligibility" or "cache_ineligibility_reasons")
                .Select(property => $"{property.Name}={property.Value.GetRawText()}"))));
    }

    private static string? FindJsonReportPath(string fixtureRoot, IReadOnlyList<string> arguments)
    {
        for (int index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == "--report" && arguments[index + 1].StartsWith("json=", StringComparison.Ordinal))
            {
                return Path.Combine(fixtureRoot, arguments[index + 1]["json=".Length..]);
            }
        }

        return null;
    }

    private static int Counter(RunSample sample, string name) => sample.Profile.GetProperty("Counters").GetProperty(name).GetInt32();

    private static int CacheCounter(RunSample sample, string name) => sample.Profile.GetProperty("Counters").GetProperty("Cache").GetProperty(name).GetInt32();

    private static int ConcurrencyCounter(RunSample sample, string name) => sample.Profile.GetProperty("Counters").GetProperty("Concurrency").GetProperty(name).GetInt32();

    private static string ConcurrencyStatus(RunSample sample) => sample.Profile.GetProperty("Counters").GetProperty("Concurrency").GetProperty("Status").GetString()!;

    private static double? AllocatedBytes(RunSample sample)
    {
        IReadOnlyList<JsonElement> profiles = sample.PairedProfiles ?? [sample.Profile];
        double total = 0;
        foreach (JsonElement profile in profiles)
        {
            if (!profile.TryGetProperty("Measurements", out JsonElement measurements)
                || !measurements.TryGetProperty("AllocatedBytesTotal", out JsonElement allocated)
                || allocated.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            total += allocated.GetDouble();
        }

        return total;
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
        return sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
    }

    private static double? MedianOptional(IEnumerable<double?> values) =>
        values.Where(static value => value.HasValue).Select(static value => value!.Value).DefaultIfEmpty().Any()
            ? Median(values.Where(static value => value.HasValue).Select(static value => value!.Value)) : null;

    private static double? Percentile95Optional(IEnumerable<double?> values) =>
        values.Where(static value => value.HasValue).Select(static value => value!.Value).DefaultIfEmpty().Any()
            ? Percentile95(values.Where(static value => value.HasValue).Select(static value => value!.Value)) : null;

    private static string CliDllPath() => Path.Combine(new ArchitectureRepositoryRootResolver().Resolve(), "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    private static string ResultsPath() => Path.Combine(new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "analysis-profile-post-optimization-results.json");

    private sealed record RunSample(double CommandTotalMs, double PreflightMs, double AnalysisOnlyMs, double OutputMs,
        string CompletionStatus, int ExitCode, bool OutputFailed, double WallClockMs, string CanonicalResult, string RawResult, JsonElement Profile,
        IReadOnlyList<JsonElement>? PairedProfiles = null);

    private sealed record SampleSeries(IReadOnlyList<RunSample> MeasuredSamples, IReadOnlyList<RunSample> PrimingSamples);

    private sealed record ScenarioSummary(string ScenarioId, string Description, int SampleCount,
        double MedianAnalysisOnlyMs, double P95AnalysisOnlyMs, double MedianOutputMs, double P95OutputMs,
        double MedianCommandTotalMs, double P95CommandTotalMs, double MedianPreflightMs,
        double MedianWallClockMs, double P95WallClockMs, double? MedianAllocatedBytes, double? P95AllocatedBytes,
        IReadOnlyList<RunSample> Samples, IReadOnlyList<RunSample> PrimingSamples);

    private sealed record BenchmarkEvidence(
        EnvironmentIdentity Environment,
        IReadOnlyList<ScenarioSummary> Scenarios,
        IReadOnlyList<RunSample> UnsuccessfulRuns,
        string SampleExclusionPolicy);

    private sealed record EnvironmentIdentity(
        string OperatingSystem, string Runtime, string Architecture, int ProcessorCount, string CliFileVersion,
        string CliAssemblySha256, string CliPackageId, string CliPackageVersion, string CliPackageSha256,
        string SourceCommit, string Configuration)
    {
        public static EnvironmentIdentity Create()
        {
            PackageIdentity package = PackageIdentity.Create(new ArchitectureRepositoryRootResolver().Resolve());
            return new EnvironmentIdentity(
                RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
                FileVersionInfo.GetVersionInfo(CliDllPath()).FileVersion ?? "unknown",
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(CliDllPath()))),
                package.Id, package.Version, package.Sha256,
                Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown", "Debug");
        }
    }

    private sealed record PackageIdentity(string Id, string Version, string Sha256)
    {
        private const string PackageId = "ArchLinterNet.Cli";
        private const string PackageExtension = ".nupkg";

        public static PackageIdentity Create(string repositoryRoot)
        {
            string packagesDirectory = Path.Combine(repositoryRoot, "nupkg");
            IReadOnlyList<string> packages = Directory.EnumerateFiles(packagesDirectory, $"{PackageId}.*{PackageExtension}")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            if (packages.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {PackageId} package in {packagesDirectory}, but found {packages.Count}. Run `rtk make pack` before recording evidence.");
            }

            string packagePath = packages[0];
            string fileName = Path.GetFileName(packagePath);
            string version = fileName[PackageId.Length..^PackageExtension.Length].TrimStart('.');
            return new PackageIdentity(
                PackageId,
                version,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(packagePath))));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;
        public string Path { get; }
        public static TemporaryDirectory Create(string prefix)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
