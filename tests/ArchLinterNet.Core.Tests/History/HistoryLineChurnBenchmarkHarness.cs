using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Tests.Benchmarking;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
[Explicit("Hardware-sensitive #1138 benchmark — run manually to capture line-churn baseline and effect evidence.")]
[Category("Benchmark")]
public sealed class HistoryLineChurnBenchmarkHarness
{
    private const int SampleCount = 5;
    private const string CalculatorPath = "src/ArchLinterNet.Core/History/Evidence/LineChurnCalculator.cs";

    [Test]
    public void CaptureBaseline()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        IReadOnlyList<BenchmarkHistoryWorkloadInput> workloads = BenchmarkHistoryInputFactory.ReadCatalog(repositoryRoot)
            .Where(static workload => workload.OwnerIssue == "#1138")
            .OrderBy(static workload => workload.Family, StringComparer.Ordinal)
            .ThenBy(static workload => workload.Dimensions["line_count"])
            .ToArray();

        Assert.That(workloads, Has.Length.EqualTo(6), "The shared #502 catalog must provide three sparse and three dense #1138 inputs.");

        List<LineChurnBaselineMeasurement> measurements = [];
        foreach (BenchmarkHistoryWorkloadInput workload in workloads)
        {
            measurements.Add(MeasureDirect(workload));
        }

        List<HistoryIngestionBaselineMeasurement> ingestionMeasurements = [];
        foreach (string family in new[] { "line_churn_sparse_edits", "line_churn_dense_edits" })
        {
            BenchmarkHistoryWorkloadInput workload = workloads.Single(candidate =>
                candidate.Family == family && candidate.Dimensions["line_count"] == 10_000);
            ingestionMeasurements.Add(MeasureIngestion(workload, measurements.Single(item => item.WorkloadId == workload.WorkloadId)));
        }

        var evidence = new
        {
            schema = "issue-1138-line-churn-baseline/v1",
            phase = "pre-change baseline",
            capturedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Git(repositoryRoot, "rev-parse", "HEAD"),
            calculatorBlob = Git(repositoryRoot, "hash-object", CalculatorPath),
            runtime = RuntimeInformation.FrameworkDescription,
            operatingSystem = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount,
            configuration = "Release",
            sampleCount = SampleCount,
            workloadCatalog = "docs/internal/large-solution-benchmarking/workload-catalog.json",
            measurements,
            ingestionMeasurements,
            memoryNotes = new
            {
                allocatedBytes = "Measured per Compute call with GC.GetAllocatedBytesForCurrentThread; median of five warmed samples.",
                retainedMemory = "No persistent calculator state is retained after Compute returns; peak temporary/working-set memory is not measured by this in-process harness.",
                deterministicDpStorage = "The existing exact implementation uses two integer rows of min(reducedOldLineCount, reducedNewLineCount) + 1 elements."
            }
        };

        string evidenceDirectory = Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "issue-1138");
        Directory.CreateDirectory(evidenceDirectory);
        string evidencePath = Path.Combine(evidenceDirectory, "baseline.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) + "\n");

        TestContext.Out.WriteLine($"Issue #1138 baseline evidence written to {evidencePath}");
        foreach (LineChurnBaselineMeasurement measurement in measurements)
        {
            TestContext.Out.WriteLine(
                $"{measurement.WorkloadId}: dpCells={measurement.BaselineDpCellUpdates}, " +
                $"medianMs={measurement.MedianMilliseconds:F3}, allocatedBytes={measurement.MedianAllocatedBytes}");
        }

        foreach (HistoryIngestionBaselineMeasurement measurement in ingestionMeasurements)
        {
            TestContext.Out.WriteLine(
                $"{measurement.WorkloadId}: directToIngestionShare={measurement.DirectToIngestionShare:P2}, " +
                $"endToEndAmdahlUpperBound={measurement.EndToEndAmdahlUpperBound:F3}x");
        }
    }

    [Test]
    public void CaptureCurrent()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        IReadOnlyList<BenchmarkHistoryWorkloadInput> workloads = BenchmarkHistoryInputFactory.ReadCatalog(repositoryRoot)
            .Where(static workload => workload.OwnerIssue == "#1138")
            .OrderBy(static workload => workload.Family, StringComparer.Ordinal)
            .ThenBy(static workload => workload.Dimensions["line_count"])
            .ToArray();
        string evidenceDirectory = Path.Combine(repositoryRoot, "docs", "internal", "large-solution-benchmarking", "issue-1138");
        using JsonDocument baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidenceDirectory, "baseline.json")));

        List<LineChurnCurrentMeasurement> measurements = [];
        foreach (BenchmarkHistoryWorkloadInput workload in workloads)
        {
            measurements.Add(MeasureCurrent(workload, baseline));
        }

        List<HistoryIngestionBaselineMeasurement> ingestionMeasurements = [];
        foreach (string family in new[] { "line_churn_sparse_edits", "line_churn_dense_edits" })
        {
            BenchmarkHistoryWorkloadInput workload = workloads.Single(candidate =>
                candidate.Family == family && candidate.Dimensions["line_count"] == 10_000);
            LineChurnBaselineMeasurement direct = measurements.Single(item => item.WorkloadId == workload.WorkloadId).Direct;
            ingestionMeasurements.Add(MeasureIngestion(workload, direct));
        }

        var evidence = new
        {
            schema = "issue-1138-line-churn-current/v1",
            phase = "post-change measurement",
            capturedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Git(repositoryRoot, "rev-parse", "HEAD"),
            calculatorBlob = Git(repositoryRoot, "hash-object", CalculatorPath),
            runtime = RuntimeInformation.FrameworkDescription,
            operatingSystem = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount,
            configuration = "Release",
            sampleCount = SampleCount,
            workloadCatalog = "docs/internal/large-solution-benchmarking/workload-catalog.json",
            baselineRevision = baseline.RootElement.GetProperty("sourceRevision").GetString(),
            measurements,
            ingestionMeasurements,
            memoryNotes = new
            {
                allocatedBytes = "Measured per uninstrumented Compute call with GC.GetAllocatedBytesForCurrentThread; median of five warmed samples.",
                retainedMemory = "No persistent calculator state is retained after Compute returns; peak temporary/working-set memory is not measured by this in-process harness.",
                boundedSearchBytes = "The fixed Myers frontier uses FrontierArrayLength × sizeof(int); it is discarded before fallback.",
                fallbackBytes = "The exact dense fallback uses two rows of DynamicProgrammingRowLength integers plus the fixed frontier during the bounded attempt."
            }
        };

        string evidencePath = Path.Combine(evidenceDirectory, "current.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) + "\n");

        TestContext.Out.WriteLine($"Issue #1138 current evidence written to {evidencePath}");
        foreach (LineChurnCurrentMeasurement measurement in measurements)
        {
            TestContext.Out.WriteLine(
                $"{measurement.WorkloadId}: fast={measurement.FastPathWork}, " +
                $"dpCells={measurement.Work.DynamicProgrammingCells}, " +
                $"medianMs={measurement.Direct.MedianMilliseconds:F3}, " +
                $"allocatedBytes={measurement.Direct.MedianAllocatedBytes}");
        }
    }

    private static LineChurnCurrentMeasurement MeasureCurrent(
        BenchmarkHistoryWorkloadInput workload,
        JsonDocument baseline)
    {
        LineChurnBaselineMeasurement direct = MeasureDirect(workload);
        JsonElement baselineMeasurement = baseline.RootElement.GetProperty("measurements")
            .EnumerateArray()
            .Single(item => item.GetProperty("workloadId").GetString() == workload.WorkloadId);
        Assert.That(direct.InputSha256, Is.EqualTo(baselineMeasurement.GetProperty("inputSha256").GetString()));
        Assert.That(direct.ExpectedOutputSha256, Is.EqualTo(baselineMeasurement.GetProperty("expectedOutputSha256").GetString()));
        Assert.That(direct.OutputDigest, Is.EqualTo(baselineMeasurement.GetProperty("outputDigest").GetString()),
            $"Canonical output changed from the pre-change baseline for {workload.WorkloadId}.");

        byte[] oldContent = Encoding.UTF8.GetBytes(workload.BeforeText ?? string.Empty);
        byte[] newContent = Encoding.UTF8.GetBytes(workload.AfterText ?? string.Empty);
        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(oldContent, newContent, out LineChurnCalculator.WorkCounts work);
        Assert.That((additions, deletions), Is.EqualTo((direct.Additions, direct.Deletions)));

        long baselineDpCells = baselineMeasurement.GetProperty("baselineDpCellUpdates").GetInt64();
        long fastPathWork = work.FrontierSteps + work.LineComparisons;
        if (workload.Family == "line_churn_sparse_edits")
        {
            Assert.That(work.UsedMyers, Is.True, $"Sparse workload {workload.WorkloadId} must complete via Myers.");
            Assert.That(work.UsedFallback, Is.False);
            Assert.That(work.DynamicProgrammingCells, Is.Zero);
            Assert.That(fastPathWork, Is.LessThan(baselineDpCells),
                $"Fast-path work must remain below baseline DP cells for {workload.WorkloadId}.");
        }
        else
        {
            Assert.That(work.UsedMyers, Is.False, $"Dense workload {workload.WorkloadId} must use exact fallback.");
            Assert.That(work.UsedFallback, Is.True);
            Assert.That(work.DynamicProgrammingCells, Is.EqualTo(baselineDpCells));
        }

        double baselineMilliseconds = baselineMeasurement.GetProperty("medianMilliseconds").GetDouble();
        long baselineAllocatedBytes = baselineMeasurement.GetProperty("medianAllocatedBytes").GetInt64();
        double timeRatio = direct.MedianMilliseconds / baselineMilliseconds;
        double allocationRatio = (double)direct.MedianAllocatedBytes / baselineAllocatedBytes;
        TestContext.Out.WriteLine(
            $"{workload.WorkloadId}: baselineMs={baselineMilliseconds:F3}, currentMs={direct.MedianMilliseconds:F3}, " +
            $"timeRatio={timeRatio:F3}, allocationRatio={allocationRatio:F3}, " +
            $"frontierSteps={work.FrontierSteps}, lineComparisons={work.LineComparisons}, " +
            $"dpCells={work.DynamicProgrammingCells}, frontierLength={work.FrontierArrayLength}");
        if (workload.WorkloadId == "synthetic-history-line-sparse-large")
        {
            Assert.That(fastPathWork, Is.LessThanOrEqualTo(baselineDpCells * 0.05d),
                "10k sparse deterministic work must be at most 5% of baseline DP cells.");
            Assert.That(timeRatio, Is.LessThanOrEqualTo(0.5d),
                "10k sparse direct median must improve by at least 2x.");
        }

        if (workload.WorkloadId == "synthetic-history-line-dense-large")
        {
            Assert.That(timeRatio, Is.LessThanOrEqualTo(1.25d),
                "10k dense direct median must stay within 1.25x of baseline.");
            Assert.That(allocationRatio, Is.LessThanOrEqualTo(1.05d),
                "10k dense allocation must stay within 105% of baseline.");
        }

        return new LineChurnCurrentMeasurement(
            workload.WorkloadId,
            direct,
            work,
            fastPathWork,
            fastPathWork + work.DynamicProgrammingCells,
            baselineDpCells,
            timeRatio,
            allocationRatio,
            work.FrontierArrayLength * sizeof(int),
            2L * work.DynamicProgrammingRowLength * sizeof(int));
    }

    private static LineChurnBaselineMeasurement MeasureDirect(BenchmarkHistoryWorkloadInput workload)
    {
        byte[] oldContent = Encoding.UTF8.GetBytes(workload.BeforeText ?? string.Empty);
        byte[] newContent = Encoding.UTF8.GetBytes(workload.AfterText ?? string.Empty);
        (long expectedAdditions, long expectedDeletions) = ReadExpectedChurn(workload);
        (int reducedOldLines, int reducedNewLines) = ReducedLineCounts(oldContent, newContent);
        long baselineDpCellUpdates = (long)reducedOldLines * reducedNewLines;

        (long additions, long deletions) = LineChurnCalculator.Compute(oldContent, newContent);
        Assert.That((additions, deletions), Is.EqualTo((expectedAdditions, expectedDeletions)),
            $"Current calculator output differs from the #502 catalog for {workload.WorkloadId}.");

        double[] elapsedMilliseconds = new double[SampleCount];
        long[] allocatedBytes = new long[SampleCount];
        for (int sample = 0; sample < SampleCount; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();
            (long measuredAdditions, long measuredDeletions) = LineChurnCalculator.Compute(oldContent, newContent);
            stopwatch.Stop();
            allocatedBytes[sample] = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            elapsedMilliseconds[sample] = stopwatch.Elapsed.TotalMilliseconds;

            Assert.That((measuredAdditions, measuredDeletions), Is.EqualTo((expectedAdditions, expectedDeletions)),
                $"Calculator output changed during the sample for {workload.WorkloadId}.");
        }

        string outputDigest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes($"{additions}\n{deletions}\n")));
        return new LineChurnBaselineMeasurement(
            workload.WorkloadId,
            workload.Family,
            workload.InputSha256,
            workload.ExpectedOutputSha256,
            workload.Dimensions["line_count"],
            workload.Dimensions["edit_count"],
            reducedOldLines,
            reducedNewLines,
            baselineDpCellUpdates,
            additions,
            deletions,
            outputDigest,
            Median(elapsedMilliseconds),
            Median(allocatedBytes));
    }

    private static HistoryIngestionBaselineMeasurement MeasureIngestion(
        BenchmarkHistoryWorkloadInput workload,
        LineChurnBaselineMeasurement direct)
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/line-churn.txt", workload.BeforeText ?? string.Empty);
        string from = repository.Commit("line churn baseline");
        repository.Write("src/line-churn.txt", workload.AfterText ?? string.Empty);
        string to = repository.Commit("line churn candidate");
        (long expectedAdditions, long expectedDeletions) = ReadExpectedChurn(workload);

        HistoryIngestionTiming warmupTiming = new();
        HistoryIngestionOutcome warmup = HistoryIngestionService.Default.Ingest(
            new HistoryIngestionRequest(repository.Path, from, to),
            warmupTiming);
        AssertIngestionResult(warmup, expectedAdditions, expectedDeletions, workload.WorkloadId);

        double[] ingestionMilliseconds = new double[SampleCount];
        for (int sample = 0; sample < SampleCount; sample++)
        {
            HistoryIngestionTiming timing = new();
            HistoryIngestionOutcome outcome = HistoryIngestionService.Default.Ingest(
                new HistoryIngestionRequest(repository.Path, from, to),
                timing);
            AssertIngestionResult(outcome, expectedAdditions, expectedDeletions, workload.WorkloadId);
            ingestionMilliseconds[sample] = ReadIngestionMilliseconds(timing);
        }

        double medianIngestionMilliseconds = Median(ingestionMilliseconds);
        double share = direct.MedianMilliseconds / medianIngestionMilliseconds;
        double? measuredShare = share is >= 0d and < 1d ? share : null;
        double? amdahlBound = measuredShare.HasValue ? 1d / (1d - measuredShare.Value) : null;
        TestContext.Out.WriteLine(
            $"{workload.WorkloadId}: directMedianMs={direct.MedianMilliseconds:F3}, " +
            $"ingestionPhaseSamplesMs=[{string.Join(",", ingestionMilliseconds.Select(static value => value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))}], " +
            $"phaseShare={(measuredShare.HasValue ? measuredShare.Value.ToString("P2", System.Globalization.CultureInfo.InvariantCulture) : "not measured (direct exceeded phase sample)")}");
        return new HistoryIngestionBaselineMeasurement(
            workload.WorkloadId,
            direct.MedianMilliseconds,
            medianIngestionMilliseconds,
            measuredShare,
            amdahlBound);
    }

    private static void AssertIngestionResult(
        HistoryIngestionOutcome outcome,
        long expectedAdditions,
        long expectedDeletions,
        string workloadId)
    {
        Assert.That(outcome.Succeeded, Is.True, $"History ingestion failed for {workloadId}: {outcome.Diagnostic?.Message}");
        LogicalFile file = outcome.Result!.LogicalFiles.Single();
        Assert.That((file.Additions, file.Deletions), Is.EqualTo((expectedAdditions, expectedDeletions)));
    }

    private static double ReadIngestionMilliseconds(HistoryIngestionTiming timing)
    {
        string formatted = timing.Format();
        string phase = formatted[(formatted.IndexOf(':') + 1)..]
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(static part => part.StartsWith("ingestion=", StringComparison.Ordinal));
        return double.Parse(phase.Split('=', 2)[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static (long Additions, long Deletions) ReadExpectedChurn(BenchmarkHistoryWorkloadInput workload)
    {
        using JsonDocument expected = JsonDocument.Parse(workload.ExpectedOutputJson);
        JsonElement values = expected.RootElement;
        return (values.GetProperty("additions").GetInt64(), values.GetProperty("deletions").GetInt64());
    }

    private static (int Old, int New) ReducedLineCounts(byte[] oldContent, byte[] newContent)
    {
        IReadOnlyList<byte[]> oldLines = LineChurnCalculator.SplitLines(oldContent);
        IReadOnlyList<byte[]> newLines = LineChurnCalculator.SplitLines(newContent);
        int prefix = 0;
        int limit = Math.Min(oldLines.Count, newLines.Count);
        while (prefix < limit && oldLines[prefix].AsSpan().SequenceEqual(newLines[prefix]))
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < limit - prefix &&
            oldLines[oldLines.Count - 1 - suffix].AsSpan().SequenceEqual(newLines[newLines.Count - 1 - suffix]))
        {
            suffix++;
        }

        return (oldLines.Count - prefix - suffix, newLines.Count - prefix - suffix);
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.Zero, $"git {string.Join(' ', arguments)} failed: {error}");
        return output.Trim();
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        return values[values.Length / 2];
    }

    private static long Median(long[] values)
    {
        Array.Sort(values);
        return values[values.Length / 2];
    }

    private sealed record LineChurnBaselineMeasurement(
        string WorkloadId,
        string Family,
        string InputSha256,
        string ExpectedOutputSha256,
        int LineCount,
        int EditCount,
        int ReducedOldLineCount,
        int ReducedNewLineCount,
        long BaselineDpCellUpdates,
        long Additions,
        long Deletions,
        string OutputDigest,
        double MedianMilliseconds,
        long MedianAllocatedBytes);

    private sealed record HistoryIngestionBaselineMeasurement(
        string WorkloadId,
        double DirectCalculatorMedianMilliseconds,
        double IngestionPhaseMedianMilliseconds,
        double? DirectToIngestionShare,
        double? EndToEndAmdahlUpperBound);

    private sealed record LineChurnCurrentMeasurement(
        string WorkloadId,
        LineChurnBaselineMeasurement Direct,
        LineChurnCalculator.WorkCounts Work,
        long FastPathWork,
        long TotalAlgorithmWork,
        long BaselineDpCellUpdates,
        double DirectTimeRatio,
        double AllocationRatio,
        long FrontierBytes,
        long DynamicProgrammingRowBytes);
}
