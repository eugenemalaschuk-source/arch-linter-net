using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("Benchmark")]
public sealed class ArchitectureBaselineMergePerformanceEvidenceTests
{
    private const int SampleCount = 5;

    [Test]
    [Explicit("Hardware-sensitive #1126 benchmark; run manually to refresh paired baseline-merge evidence.")]
    public void RunBaselineMergeEffectMatrix()
    {
        string environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
            + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}";

        foreach (MergeWorkload workload in CreateWorkloads())
        {
            ArchitectureBaselineDocument baseline = CreateBaseline(workload);
            ArchitectureContractDocument legacyPolicy = CreatePolicy(workload);
            ArchitectureBaselineMergeLegacyReference.LegacyWorkCounter legacyWork = new();
            ArchitectureBaselineMergeLegacyReference.MergeStrict(legacyPolicy, baseline, legacyWork);
            string legacyDigest = ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(legacyPolicy);

            _ = MeasureCurrent(workload, baseline);
            _ = MeasureCurrent(workload, baseline);
            Measurement[] samples = Enumerable.Range(0, SampleCount)
                .Select(_ => MeasureCurrent(workload, baseline))
                .ToArray();
            Assert.That(samples.Select(sample => sample.Digest), Is.All.EqualTo(legacyDigest),
                $"Current merge output differs from the prior linear behavior for {workload.Name}.");

            ArchitectureContractDocument indexedPolicy = CreatePolicy(workload);
            ArchitectureBaselineMergeWorkCounter indexedWork = new();
            ArchitectureBaselineLoadingService.Merge(indexedPolicy, baseline, indexedWork);
            Assert.That(ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(indexedPolicy), Is.EqualTo(legacyDigest),
                $"Counted merge output differs from the prior linear behavior for {workload.Name}.");

            long retainedBytes = MeasureRetainedOutput(workload, baseline);
            MergeEvidenceRow row = new(
                workload.Name,
                workload.Version,
                workload.ContractCount,
                workload.BaselineEntryCount,
                workload.InlineIgnoreCount,
                workload.LoadedIgnoreCount,
                SampleCount,
                Median(samples.Select(sample => sample.ElapsedMilliseconds)),
                Median(samples.Select(sample => sample.AllocatedBytes)),
                retainedBytes,
                legacyWork.ContractIdComparisons,
                legacyWork.IgnoreIdentityComparisons,
                legacyWork.LoadedIgnoresAdded,
                indexedWork.ContractIndexEntries,
                indexedWork.ContractLinearScanComparisons,
                indexedWork.ContractLookups,
                indexedWork.InlineIgnoreIndexEntries,
                indexedWork.MembershipChecks,
                indexedWork.LoadedIgnoresAccepted,
                legacyDigest,
                environment);
            TestContext.Out.WriteLine(JsonSerializer.Serialize(row));
        }
    }

    private static IEnumerable<MergeWorkload> CreateWorkloads()
    {
        yield return new MergeWorkload("small-v1", 1, 8, 8, 2, 4, WorkloadKind.Small);
        yield return new MergeWorkload("small-v3", 3, 8, 8, 2, 4, WorkloadKind.Small);

        foreach (int count in new[] { 32, 128, 512 })
        {
            yield return new MergeWorkload($"contracts-{count}", 1, count, 64, 0, 0, WorkloadKind.Contracts);
            yield return new MergeWorkload($"entries-{count}", 1, 1, count, 0, 0, WorkloadKind.Entries);
        }

        foreach (int totalIgnoreCount in new[] { 100, 500, 2_000 })
        {
            int inlineCount = totalIgnoreCount / 2;
            int loadedCount = totalIgnoreCount - inlineCount;
            yield return new MergeWorkload(
                $"legacy-inline-loaded-{totalIgnoreCount}", 1, 1, 1, inlineCount, loadedCount, WorkloadKind.Ignores);
            yield return new MergeWorkload(
                $"structured-inline-loaded-{totalIgnoreCount}", 3, 1, 1, inlineCount, loadedCount, WorkloadKind.Ignores);
        }
    }

    private static ArchitectureContractDocument CreatePolicy(MergeWorkload workload)
    {
        List<ArchitectureDependencyContract> contracts = Enumerable.Range(0, workload.ContractCount)
            .Select(index => new ArchitectureDependencyContract
            {
                Id = ContractId(index),
                Name = ContractId(index),
                Source = "Synthetic.Source",
                Forbidden = new List<string>(),
            })
            .ToList();

        for (int index = 0; index < workload.InlineIgnoreCount; index++)
        {
            contracts[0].IgnoredViolations.Add(ToPolicyIgnore(CreateIgnore(workload.Version, index)));
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Synthetic baseline merge workload",
            Contracts = new ArchitectureContractGroups { Strict = contracts },
        };
    }

    private static ArchitectureBaselineDocument CreateBaseline(MergeWorkload workload)
    {
        int targetContractIndex = workload.Kind == WorkloadKind.Contracts ? workload.ContractCount - 1 : 0;
        List<ArchitectureBaselineContractEntry> entries = new();
        for (int index = 0; index < workload.BaselineEntryCount; index++)
        {
            List<ArchitectureBaselineIgnoredViolation> ignores = index == 0
                ? Enumerable.Range(workload.InlineIgnoreCount, workload.LoadedIgnoreCount)
                    .Select(ignoreIndex => CreateIgnore(workload.Version, ignoreIndex))
                    .ToList()
                : new List<ArchitectureBaselineIgnoredViolation>();
            entries.Add(new ArchitectureBaselineContractEntry
            {
                Id = ContractId(targetContractIndex),
                IgnoredViolations = ignores,
            });
        }

        return new ArchitectureBaselineDocument
        {
            Version = workload.Version,
            Baseline = new ArchitectureBaselineContractGroups { Strict = entries },
        };
    }

    private static ArchitectureBaselineIgnoredViolation CreateIgnore(int version, int index)
    {
        ArchitectureBaselineIgnoredViolation ignore = new()
        {
            SourceType = $"Synthetic.SourceType{index:D5}",
            ForbiddenReference = $"Synthetic.ForbiddenType{index:D5}",
            Reason = $"synthetic reason {index}",
        };

        if (version is 2 or 3)
        {
            ignore.IdentityVersion = ArchitectureViolationIdentity.CurrentVersion;
            ignore.ContractFamily = "dependency";
            ignore.Kind = "dependency";
            ignore.SourceAssembly = "Synthetic.SourceAssembly";
            ignore.SourceMember = $"SourceMember{index:D5}";
            ignore.TargetAssembly = "Synthetic.TargetAssembly";
            ignore.TargetType = $"Synthetic.TargetType{index:D5}";
            ignore.TargetMember = $"TargetMember{index:D5}";
            ignore.Occurrence = index;
            ignore.Configuration = "release";
        }

        return ignore;
    }

    private static ArchitectureIgnoredViolation ToPolicyIgnore(ArchitectureBaselineIgnoredViolation baselineIgnore)
    {
        return new ArchitectureIgnoredViolation
        {
            SourceType = baselineIgnore.SourceType,
            ForbiddenReference = baselineIgnore.ForbiddenReference,
            Reason = baselineIgnore.Reason,
            IdentityVersion = baselineIgnore.IdentityVersion,
            ContractFamily = baselineIgnore.ContractFamily,
            Kind = baselineIgnore.Kind,
            SourceAssembly = baselineIgnore.SourceAssembly,
            SourceMember = baselineIgnore.SourceMember,
            TargetAssembly = baselineIgnore.TargetAssembly,
            TargetType = baselineIgnore.TargetType,
            TargetMember = baselineIgnore.TargetMember,
            Occurrence = baselineIgnore.Occurrence,
            Configuration = baselineIgnore.Configuration,
        };
    }

    private static Measurement MeasureCurrent(
        MergeWorkload workload,
        ArchitectureBaselineDocument baseline)
    {
        ArchitectureContractDocument policy = CreatePolicy(workload);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        ArchitectureBaselineLoadingService.Merge(policy, baseline);
        stopwatch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string digest = ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(policy);
        return new Measurement(stopwatch.Elapsed.TotalMilliseconds, allocatedBytes, digest);
    }

    private static long MeasureRetainedOutput(
        MergeWorkload workload,
        ArchitectureBaselineDocument baseline)
    {
        ArchitectureContractDocument policy = CreatePolicy(workload);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        ArchitectureBaselineLoadingService.Merge(policy, baseline);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true) - retainedBefore;
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static long Median(IEnumerable<long> values)
    {
        long[] ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static string ContractId(int index) => $"Synthetic.Rule{index:D5}";

    private enum WorkloadKind
    {
        Small,
        Contracts,
        Entries,
        Ignores,
    }

    private sealed record MergeWorkload(
        string Name,
        int Version,
        int ContractCount,
        int BaselineEntryCount,
        int InlineIgnoreCount,
        int LoadedIgnoreCount,
        WorkloadKind Kind);

    private sealed record Measurement(double ElapsedMilliseconds, long AllocatedBytes, string Digest);

    private sealed record MergeEvidenceRow(
        string Workload,
        int Version,
        int ContractCount,
        int BaselineEntryCount,
        int InlineIgnoreCount,
        int LoadedIgnoreCount,
        int SampleCount,
        double CurrentMedianMilliseconds,
        long CurrentMedianAllocatedBytes,
        long RetainedOutputDeltaBytes,
        long LegacyContractIdComparisons,
        long LegacyIgnoreIdentityComparisons,
        long LegacyLoadedIgnoresAdded,
        long IndexedContractIndexEntries,
        long IndexedContractLinearScanComparisons,
        long IndexedContractLookups,
        long IndexedInlineIgnoreIndexEntries,
        long IndexedMembershipChecks,
        long IndexedLoadedIgnoresAccepted,
        string CanonicalIgnoreSha256,
        string Environment);
}
