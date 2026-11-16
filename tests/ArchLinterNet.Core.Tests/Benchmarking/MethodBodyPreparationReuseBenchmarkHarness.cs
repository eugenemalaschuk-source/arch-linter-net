using System.Diagnostics;
using System.Runtime.InteropServices;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
[NonParallelizable]
[Explicit("Issue #1130 hardware-sensitive before/after evidence; uses the #502 synthetic fixture generator.")]
[Category("Benchmark")]
public sealed class MethodBodyPreparationReuseBenchmarkHarness
{
    private const int SampleCount = 3;

    [Test]
    [CancelAfter(300_000)]
    public void MeasureSameInputMethodBodyRuleScaling()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        TestContext.Out.WriteLine($"revision={ReadRevision(repositoryRoot)}");
        TestContext.Out.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        TestContext.Out.WriteLine($"os={RuntimeInformation.OSDescription}");
        TestContext.Out.WriteLine($"processors={Environment.ProcessorCount}; gc_server={System.Runtime.GCSettings.IsServerGC}");
        TestContext.Out.WriteLine(
            "shape: Q=method-body rules; P=1 project; T=32 types; S=8 source files; L=1 selected layer; " +
            "R=0 project references; RealMsBuild; max_parallelism=1; strict validation.");

        foreach (int ruleCount in new[] { 1, 5, 20 })
        {
            BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-issue-1130-method-body-q{ruleCount}",
                BenchmarkTopologyShape.ManyProjectsFewTypes,
                new BenchmarkDimensionSet
                {
                    ProjectCount = 1,
                    TypesPerProject = 32,
                    SourceFilesPerProject = 8,
                    ReferencesPerProject = 0,
                    LayerCount = 1,
                    SelectorPredicateTermsPerLayer = 1,
                    ContractsPerWorkload = 1,
                    SourceRootCount = 1,
                    MethodBodyRuleCount = ruleCount,
                });

            using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
            fixture.Build();

            // Prime JIT, MSBuild/Buildalyzer, and filesystem caches outside the reported samples.
            _ = BenchmarkAuditAnalysisRunner.Run(
                fixture,
                observerEnabled: true,
                sampleOrdinal: 0,
                isWarmSample: false,
                parallelMode: "sequential",
                maxParallelism: 1,
                BenchmarkResourceMeasurement.Unavailable("Fixture materialization excluded."),
                BenchmarkResourceMeasurement.Available(1, "milliseconds"));

            List<Sample> samples = new(SampleCount);
            for (int sampleOrdinal = 1; sampleOrdinal <= SampleCount; sampleOrdinal++)
            {
                BenchmarkAuditAnalysisRun run = BenchmarkAuditAnalysisRunner.Run(
                    fixture,
                    observerEnabled: true,
                    sampleOrdinal,
                    isWarmSample: true,
                    parallelMode: "sequential",
                    maxParallelism: 1,
                    BenchmarkResourceMeasurement.Unavailable("Fixture materialization excluded."),
                    BenchmarkResourceMeasurement.Available(1, "milliseconds"));
                decimal methodBodyMilliseconds = ReadPhaseMilliseconds(run, "method_body");
                samples.Add(new Sample(
                    run.Sample.MeasuredWallClockMilliseconds!.Value,
                    methodBodyMilliseconds,
                    run.Sample.AllocatedBytes.Value!.Value,
                    run.ManagedHeapDeltaAfterEvaluationBytes,
                    run.Outcome.Violations.Count,
                    run.CanonicalResult.Sha256,
                    run.ObservedWorkCounters));
            }

            Assert.That(samples.Select(sample => sample.CanonicalResult).Distinct().Count(), Is.EqualTo(1),
                $"Q={ruleCount} must produce the same canonical strict result across paired samples.");
            TestContext.Out.WriteLine(
                $"Q={ruleCount}; workload={workload.WorkloadIdentity}; " +
                $"findings={samples[0].FindingCount}; canonical={samples[0].CanonicalResult}; " +
                $"wall_ms_median={Median(samples, sample => sample.WallMilliseconds):F2}; " +
                $"method_body_ms_median={Median(samples, sample => sample.MethodBodyMilliseconds):F2}; " +
                $"method_body_share_median={Median(samples, sample => sample.MethodBodyMilliseconds / sample.WallMilliseconds):P2}; " +
                $"allocated_bytes_median={Median(samples, sample => sample.AllocatedBytes)}; " +
                $"live_heap_delta_after_evaluation_bytes_median={Median(samples, sample => sample.LiveHeapDeltaBytes)}");
            foreach (Sample sample in samples)
            {
                TestContext.Out.WriteLine(
                    $"  sample canonical={sample.CanonicalResult}; wall_ms={sample.WallMilliseconds:F2}; " +
                    $"method_body_ms={sample.MethodBodyMilliseconds:F2}; allocated_bytes={sample.AllocatedBytes}; " +
                    $"live_heap_delta_bytes={sample.LiveHeapDeltaBytes}; findings={sample.FindingCount}; " +
                    $"work={System.Text.Json.JsonSerializer.Serialize(sample.WorkCounters)}");
            }
        }
    }

    private static decimal ReadPhaseMilliseconds(BenchmarkAuditAnalysisRun run, string phaseName)
    {
        foreach (System.Text.Json.JsonElement phase in run.Sample.RawAnalysisProfile
                     .GetProperty("Phases").EnumerateArray())
        {
            if (string.Equals(phase.GetProperty("Name").GetString(), phaseName, StringComparison.Ordinal))
                return phase.GetProperty("ElapsedMs").GetDecimal();
        }

        Assert.Fail($"The analysis profile did not contain the '{phaseName}' phase.");
        return 0;
    }

    private static string ReadRevision(string repositoryRoot)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output;
    }

    private static decimal Median(IReadOnlyList<Sample> samples, Func<Sample, decimal> select) =>
        samples.Select(select).Order().ElementAt(samples.Count / 2);

    private static long Median(IReadOnlyList<Sample> samples, Func<Sample, long> select) =>
        samples.Select(select).Order().ElementAt(samples.Count / 2);

    private sealed record Sample(
        decimal WallMilliseconds,
        decimal MethodBodyMilliseconds,
        long AllocatedBytes,
        long LiveHeapDeltaBytes,
        int FindingCount,
        string CanonicalResult,
        IReadOnlyDictionary<string, long> WorkCounters);
}
