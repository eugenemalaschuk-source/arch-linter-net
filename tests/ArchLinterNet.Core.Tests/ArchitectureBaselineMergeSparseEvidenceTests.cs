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
public sealed class ArchitectureBaselineMergeSparseEvidenceTests
{
    private const int SampleCount = 5;

    [Test]
    [Explicit("Hardware-sensitive #1126 review measurements; run manually after changing sparse lookup strategy.")]
    public void RunSparseAndImportedOnlyEffectMatrix()
    {
        string environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
            + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}";

        foreach (SparseWorkload workload in CreateWorkloads())
        {
            ArchitectureBaselineDocument baseline = CreateBaseline(workload);
            _ = Measure(workload, baseline, useCurrent: false);
            _ = Measure(workload, baseline, useCurrent: true);

            Sample[] currentSamples = new Sample[SampleCount];
            Sample[] legacySamples = new Sample[SampleCount];
            for (int sampleIndex = 0; sampleIndex < SampleCount; sampleIndex++)
            {
                // Alternate execution order to reduce drift from process warmth and CPU load.
                if (sampleIndex % 2 == 0)
                {
                    legacySamples[sampleIndex] = Measure(workload, baseline, useCurrent: false);
                    currentSamples[sampleIndex] = Measure(workload, baseline, useCurrent: true);
                }
                else
                {
                    currentSamples[sampleIndex] = Measure(workload, baseline, useCurrent: true);
                    legacySamples[sampleIndex] = Measure(workload, baseline, useCurrent: false);
                }
            }

            string legacyDigest = legacySamples[0].Digest;
            Assert.That(currentSamples.Select(sample => sample.Digest), Is.All.EqualTo(legacyDigest),
                $"Current merge output differs from the prior linear behavior for {workload.Name}.");
            Assert.That(legacySamples.Select(sample => sample.Digest), Is.All.EqualTo(legacyDigest),
                $"Linear reference output differs across samples for {workload.Name}.");

            Sample current = currentSamples[0];
            Sample legacy = legacySamples[0];
            SparseEvidenceRow row = new(
                workload.Name,
                workload.GroupKind.ToString(),
                workload.ContractCount,
                workload.BaselineEntryCount,
                workload.BaselineEntryCount * workload.ImportedDiagnosticCount,
                SampleCount,
                Median(legacySamples.Select(sample => sample.ElapsedMilliseconds)),
                Median(currentSamples.Select(sample => sample.ElapsedMilliseconds)),
                Median(legacySamples.Select(sample => sample.AllocatedBytes)),
                Median(currentSamples.Select(sample => sample.AllocatedBytes)),
                legacy.LegacyContractIdComparisons,
                current.ContractIndexEntries,
                current.ContractLookups,
                current.ContractLinearScanComparisons,
                current.MembershipChecks,
                legacyDigest,
                environment);
            TestContext.Out.WriteLine(JsonSerializer.Serialize(row));
        }
    }

    private static IEnumerable<SparseWorkload> CreateWorkloads()
    {
        yield return new SparseWorkload("first-match-c512-b1", GroupKind.Strict, 512, 1, 0, TargetLastContract: false);
        yield return new SparseWorkload("first-match-c512-b4", GroupKind.Strict, 512, 4, 0, TargetLastContract: false);
        yield return new SparseWorkload("last-match-c512-b1", GroupKind.Strict, 512, 1, 0, TargetLastContract: true);
        yield return new SparseWorkload("last-match-c512-b4", GroupKind.Strict, 512, 4, 0, TargetLastContract: true);
        yield return new SparseWorkload("imported-only-c512-b1", GroupKind.ImportedExternal, 512, 1, 64);
        yield return new SparseWorkload("imported-only-c512-b64", GroupKind.ImportedExternal, 512, 64, 64);
    }

    private static ArchitectureContractDocument CreatePolicy(SparseWorkload workload)
    {
        if (workload.GroupKind == GroupKind.Strict)
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

            return new ArchitectureContractDocument
            {
                Version = 1,
                Name = "Sparse baseline merge review workload",
                Contracts = new ArchitectureContractGroups { Strict = contracts },
            };
        }

        List<ArchitectureExternalDependencyContract> externalContracts = Enumerable.Range(0, workload.ContractCount)
            .Select(index => new ArchitectureExternalDependencyContract
            {
                Id = $"Synthetic.ExternalContract{index:D5}",
                Name = $"Synthetic external contract {index}",
                Source = "Synthetic.Layer",
                Forbidden = new List<string>(),
            })
            .ToList();
        List<ArchitectureExternalEvidenceRequirement> evidence = Enumerable.Range(0, workload.BaselineEntryCount)
            .Select(index => new ArchitectureExternalEvidenceRequirement
            {
                Id = EvidenceId(index),
                Format = "sarif",
                Required = true,
                Tool = "Synthetic Analyzer",
                Run = "synthetic-run",
            })
            .ToList();

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Imported diagnostic baseline review workload",
            Contracts = new ArchitectureContractGroups { StrictExternal = externalContracts },
            ExternalEvidence = evidence,
        };
    }

    private static ArchitectureBaselineDocument CreateBaseline(SparseWorkload workload)
    {
        if (workload.GroupKind == GroupKind.Strict)
        {
            return new ArchitectureBaselineDocument
            {
                Version = 1,
                Baseline = new ArchitectureBaselineContractGroups
                {
                    Strict = Enumerable.Range(0, workload.BaselineEntryCount)
                        .Select(_ => new ArchitectureBaselineContractEntry
                        {
                            Id = ContractId(workload.TargetLastContract ? workload.ContractCount - 1 : 0),
                            IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>(),
                        })
                        .ToList(),
                },
            };
        }

        return new ArchitectureBaselineDocument
        {
            Version = 3,
            Baseline = new ArchitectureBaselineContractGroups
            {
                StrictExternal = Enumerable.Range(0, workload.BaselineEntryCount)
                    .Select(index => new ArchitectureBaselineContractEntry
                    {
                        Id = EvidenceId(index),
                        IgnoredViolations = Enumerable.Range(0, workload.ImportedDiagnosticCount)
                            .Select(diagnosticIndex => new ArchitectureBaselineIgnoredViolation
                            {
                                SourceType = $"Synthetic.ImportedType{index:D3}.{diagnosticIndex:D3}",
                                ForbiddenReference = "Synthetic.External.Diagnostic",
                                Reason = "synthetic imported diagnostic",
                                IdentityVersion = ArchitectureViolationIdentity.CurrentVersion,
                                ContractFamily = "external",
                                Kind = "external_diagnostic",
                                SourceAssembly = "Synthetic.ExternalTool",
                                SourceMember = $"Diagnostic{diagnosticIndex:D3}",
                                TargetAssembly = "Synthetic.Project",
                                TargetType = "Synthetic.Target",
                                TargetMember = "Analyze",
                                Occurrence = diagnosticIndex,
                                Configuration = "release",
                            })
                            .ToList(),
                    })
                    .ToList(),
            },
        };
    }

    private static Sample Measure(SparseWorkload workload, ArchitectureBaselineDocument baseline, bool useCurrent)
    {
        ArchitectureContractDocument policy = CreatePolicy(workload);
        ArchitectureBaselineMergeWorkCounter indexedWork = new();
        ArchitectureBaselineMergeLegacyReference.LegacyWorkCounter legacyWork = new();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (useCurrent)
        {
            ArchitectureBaselineLoadingService.Merge(policy, baseline, indexedWork);
        }
        else if (workload.GroupKind == GroupKind.Strict)
        {
            ArchitectureBaselineMergeLegacyReference.MergeStrict(policy, baseline, legacyWork);
        }
        else
        {
            ArchitectureBaselineMergeLegacyReference.MergeImportedExternal(policy, baseline);
        }

        stopwatch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string digest = workload.GroupKind == GroupKind.Strict
            ? ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(policy)
            : ArchitectureBaselineMergeLegacyReference.StrictExternalIgnoreDigest(policy);
        return new Sample(
            stopwatch.Elapsed.TotalMilliseconds,
            allocatedBytes,
            digest,
            legacyWork.ContractIdComparisons,
            indexedWork.ContractIndexEntries,
            indexedWork.ContractLookups,
            indexedWork.ContractLinearScanComparisons,
            indexedWork.MembershipChecks);
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

    private static string EvidenceId(int index) => $"Synthetic.Evidence{index:D5}";

    private enum GroupKind
    {
        Strict,
        ImportedExternal,
    }

    private sealed record SparseWorkload(
        string Name,
        GroupKind GroupKind,
        int ContractCount,
        int BaselineEntryCount,
        int ImportedDiagnosticCount,
        bool TargetLastContract = false);

    private sealed record Sample(
        double ElapsedMilliseconds,
        long AllocatedBytes,
        string Digest,
        long LegacyContractIdComparisons,
        long ContractIndexEntries,
        long ContractLookups,
        long ContractLinearScanComparisons,
        long MembershipChecks);

    private sealed record SparseEvidenceRow(
        string Workload,
        string Group,
        int ContractCount,
        int BaselineEntryCount,
        int ImportedDiagnosticCount,
        int SampleCount,
        double LegacyMedianMilliseconds,
        double CurrentMedianMilliseconds,
        long LegacyMedianAllocatedBytes,
        long CurrentMedianAllocatedBytes,
        long LegacyContractIdComparisons,
        long IndexedContractIndexEntries,
        long IndexedContractLookups,
        long IndexedContractLinearScanComparisons,
        long IndexedMembershipChecks,
        string CanonicalIgnoreSha256,
        string Environment);
}
