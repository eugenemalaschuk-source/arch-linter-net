using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("Benchmark")]
public sealed partial class ArchitectureIgnoreMatcherPerformanceEvidenceTests
{
    private const int SampleCount = 5;

    [Test]
    [Explicit("Hardware-sensitive #1127 evidence run; excluded from normal test and acceptance targets.")]
    public void RunCurrentMatcherBaseline()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string revision = CurrentRevision(root);
        string environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
            + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}";
        var evidence = new List<object>();

        var metadata = new
        {
            Evidence = "architecture-ignore-matching-before",
            Revision = revision,
            OrderedReferenceSourceSha256 = OrderedReferenceSourceSha256(root),
            Matcher = "Frozen copy of the original ordered matcher, retained for repeatable pre-index measurements.",
            Environment = environment,
            WorkloadDefinition = "Ignore entries (I), findings (F), required matches (K), and wildcard entries (W) vary independently; each sample reuses one contract ignore list and one tracker.",
            PhaseShare = "100% of this focused inner workload is matcher calls; product validation phase share is not measured because the current profile does not attribute matcher time separately.",
            EndToEndBound = "A product-wide Amdahl bound is not measured. No full-validation speedup is inferred from this isolated workload.",
        };
        TestContext.Out.WriteLine(JsonSerializer.Serialize(metadata));

        foreach (MatcherWorkload workload in CreateWorkloads())
        {
            MatchResult warmup = RunCurrent(workload);
            var samples = new Measurement[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                samples[i] = MeasureCurrent(workload);
            }

            string checksum = Checksum(warmup);
            Assert.That(samples.Select(sample => sample.ResultChecksum), Is.All.EqualTo(checksum),
                $"Matcher results changed across samples for '{workload.Name}'.");

            var row = new
            {
                Workload = workload.Name,
                Kind = workload.Kind.ToString(),
                InputSha256 = InputChecksum(workload),
                IgnoreEntriesI = workload.Ignores.Count,
                FindingsF = workload.Findings.Length,
                RequiredMatchesK = workload.RequiredMatches,
                WildcardEntriesW = workload.WildcardCount,
                SampleCount,
                MedianMatcherMilliseconds = Median(samples.Select(sample => sample.ElapsedMilliseconds)),
                MedianAllocatedBytes = Median(samples.Select(sample => sample.AllocatedBytes)),
                RetainedIndexBytes = "not applicable: baseline implementation retains no per-contract matcher index",
                LegacyEntryEvaluations = (long)workload.Ignores.Count * workload.Findings.Length,
                StructuredFingerprintCreations = workload.Kind == MatcherKind.StructuredWaiver
                    ? (long)workload.Ignores.Count * workload.Findings.Length
                    : 0,
                WildcardPatternEvaluations = (long)workload.WildcardCount * workload.Findings.Length,
                CanonicalResultSha256 = checksum,
            };
            evidence.Add(row);
            TestContext.Out.WriteLine(JsonSerializer.Serialize(row));
        }

        string evidenceDirectory = Path.Combine(root, "docs", "internal", "large-solution-benchmarking", "evidence", "issue-1127");
        Directory.CreateDirectory(evidenceDirectory);
        string evidencePath = Path.Combine(evidenceDirectory, "before.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new { metadata, Workloads = evidence },
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        TestContext.Progress.WriteLine($"Wrote baseline evidence to {Path.GetRelativePath(root, evidencePath)}");
    }

    private static IEnumerable<MatcherWorkload> CreateWorkloads()
    {
        yield return CreateWorkload("small-i8-f8-k8", MatcherKind.VersionedIdentity, 8, 8, 1, 0);

        foreach (int ignores in new[] { 64, 256, 1_024 })
        {
            yield return CreateWorkload($"identity-ignore-scale-i{ignores}-f64-k64", MatcherKind.VersionedIdentity,
                ignores, 64, 1, 0);
            yield return CreateWorkload($"waiver-ignore-scale-i{ignores}-f64-k64", MatcherKind.StructuredWaiver,
                ignores, 64, 1, 0);
            yield return CreateWorkload($"literal-ignore-scale-i{ignores}-f64-k64", MatcherKind.LiteralPair,
                ignores, 64, 1, 0);
        }

        foreach (int findings in new[] { 16, 64, 256 })
        {
            yield return CreateWorkload($"identity-finding-scale-i1024-f{findings}-k{findings}",
                MatcherKind.VersionedIdentity, 1_024, findings, 1, 0);
        }

        foreach (int duplicatesPerFinding in new[] { 1, 4, 16 })
        {
            int findings = 32;
            int matches = findings * duplicatesPerFinding;
            yield return CreateWorkload($"identity-match-scale-i1024-f{findings}-k{matches}",
                MatcherKind.VersionedIdentity, 1_024, findings, duplicatesPerFinding, 0);
        }

        foreach (int wildcards in new[] { 0, 64, 256 })
        {
            yield return CreateWorkload($"wildcard-scale-i64-w{wildcards}-f64-k64", MatcherKind.VersionedIdentity,
                64, 64, 1, wildcards);
        }
    }

    private static MatcherWorkload CreateWorkload(
        string name,
        MatcherKind kind,
        int exactIgnoreCount,
        int findingCount,
        int duplicatesPerFinding,
        int wildcardCount)
    {
        int requiredMatches = findingCount * duplicatesPerFinding;
        if (requiredMatches > exactIgnoreCount)
        {
            throw new ArgumentOutOfRangeException(nameof(duplicatesPerFinding), "The ignore count must contain every requested matching entry.");
        }

        ArchitectureViolationIdentity[] findings = Enumerable.Range(0, findingCount)
            .Select(CreateIdentity)
            .ToArray();
        List<ArchitectureIgnoredViolation> ignores = new(exactIgnoreCount + wildcardCount);

        for (int index = 0; index < exactIgnoreCount; index++)
        {
            int findingIndex = index < requiredMatches
                ? index / duplicatesPerFinding
                : findingCount + index;
            ArchitectureViolationIdentity identity = CreateIdentity(findingIndex);
            ignores.Add(CreateIgnore(identity, kind, index));
        }

        for (int index = 0; index < wildcardCount; index++)
        {
            ignores.Add(new ArchitectureIgnoredViolation
            {
                SourceType = $"Never.Source.**.{index}",
                ForbiddenReference = "Never.Target.**",
            });
        }

        return new MatcherWorkload(name, kind, ignores, findings, requiredMatches, wildcardCount);
    }

    private static ArchitectureIgnoredViolation CreateIgnore(
        ArchitectureViolationIdentity identity,
        MatcherKind kind,
        int index)
    {
        ArchitectureIgnoredViolation ignore = new()
        {
            SourceType = identity.SourceType,
            ForbiddenReference = $"Target.Type.{IdentityIndex(identity)}",
            IsFromLoadedBaseline = index % 2 == 0,
        };

        if (kind == MatcherKind.VersionedIdentity)
        {
            ignore.IdentityVersion = ArchitectureViolationIdentity.CurrentVersion;
            ignore.ContractFamily = identity.ContractFamily;
            ignore.Kind = identity.Kind;
            ignore.SourceAssembly = identity.SourceAssembly;
            ignore.SourceMember = identity.SourceMember;
            ignore.TargetAssembly = identity.TargetAssembly;
            ignore.TargetType = identity.TargetType;
            ignore.TargetMember = identity.TargetMember;
            ignore.Occurrence = identity.Occurrence;
            ignore.Configuration = identity.Configuration;
        }
        else if (kind == MatcherKind.StructuredWaiver)
        {
            ignore.WaiverId = $"waiver-{index}";
            ignore.Target = new ArchitectureWaiverTarget
            {
                Fingerprint = ArchitectureWaiverTargetFingerprint.Create(identity),
            };
        }

        return ignore;
    }

    private static ArchitectureViolationIdentity CreateIdentity(int index)
    {
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            "strict",
            "dependency",
            "benchmark-contract",
            $"SourceAssembly.{index}",
            $"Source.Type.{index}",
            "Execute",
            $"TargetAssembly.{index}",
            $"Target.Type.{index}",
            "Create",
            index);
    }

    private static int IdentityIndex(ArchitectureViolationIdentity identity)
    {
        return int.Parse(identity.SourceType["Source.Type.".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static MatchResult RunCurrent(MatcherWorkload workload)
    {
        ArchitectureIgnoreUsageTracker tracker = new();
        bool[] ignored = new bool[workload.Findings.Length];
        bool[] loadedBaseline = new bool[workload.Findings.Length];

        for (int index = 0; index < workload.Findings.Length; index++)
        {
            ArchitectureViolationIdentity finding = workload.Findings[index];
            ignored[index] = ArchitectureIgnoreMatcherTestReference.IsIgnored(
                finding.SourceType,
                $"Target.Type.{index}",
                workload.Ignores,
                tracker,
                finding,
                out loadedBaseline[index]);
        }

        return new MatchResult(ignored, loadedBaseline, tracker.MatchedIndexes.Order().ToArray());
    }

    private static Measurement MeasureCurrent(MatcherWorkload workload)
    {
        ArchitectureIgnoreUsageTracker tracker = new();
        bool[] ignored = new bool[workload.Findings.Length];
        bool[] loadedBaseline = new bool[workload.Findings.Length];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        for (int index = 0; index < workload.Findings.Length; index++)
        {
            ArchitectureViolationIdentity finding = workload.Findings[index];
            ignored[index] = ArchitectureIgnoreMatcherTestReference.IsIgnored(
                finding.SourceType,
                $"Target.Type.{index}",
                workload.Ignores,
                tracker,
                finding,
                out loadedBaseline[index]);
        }

        stopwatch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new Measurement(stopwatch.Elapsed.TotalMilliseconds, allocatedBytes,
            Checksum(new MatchResult(ignored, loadedBaseline, tracker.MatchedIndexes.Order().ToArray())));
    }

    private static string Checksum(MatchResult result)
    {
        StringBuilder canonical = new();
        foreach (bool suppressed in result.Suppressed)
        {
            canonical.Append(suppressed ? '1' : '0');
        }

        canonical.Append('|');
        foreach (bool loaded in result.MatchedByLoadedBaseline)
        {
            canonical.Append(loaded ? '1' : '0');
        }

        canonical.Append('|').AppendJoin(',', result.MatchedIndexes);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string InputChecksum(MatcherWorkload workload)
    {
        string canonical = JsonSerializer.Serialize(new
        {
            workload.Name,
            Kind = workload.Kind.ToString(),
            workload.Findings,
            Ignores = workload.Ignores.Select(ignore => new
            {
                ignore.SourceType,
                ignore.ForbiddenReference,
                ignore.WaiverId,
                TargetFingerprint = ignore.Target?.Fingerprint,
                ignore.Owner,
                ignore.Issue,
                ignore.Introduced,
                ignore.Expires,
                ignore.WaiverValidationError,
                ignore.IdentityVersion,
                ignore.ContractFamily,
                ignore.Kind,
                ignore.SourceAssembly,
                ignore.SourceMember,
                ignore.TargetAssembly,
                ignore.TargetType,
                ignore.TargetMember,
                ignore.Occurrence,
                ignore.Configuration,
                ignore.IsFromLoadedBaseline,
            }),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CurrentRevision(string root)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "rev-parse", "HEAD" },
        }) ?? throw new InvalidOperationException("Could not start git to identify the benchmark revision.");

        string revision = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(revision))
        {
            throw new InvalidOperationException("Could not determine the benchmark revision.");
        }

        return revision;
    }

    private static string OrderedReferenceSourceSha256(string root)
    {
        string referencePath = Path.Combine(root, "tests", "ArchLinterNet.Core.Tests", "ArchitectureIgnoreMatcherTestReference.cs");
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(referencePath)));
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

    private enum MatcherKind
    {
        VersionedIdentity,
        StructuredWaiver,
        LiteralPair,
    }

    private sealed record MatcherWorkload(
        string Name,
        MatcherKind Kind,
        IReadOnlyList<ArchitectureIgnoredViolation> Ignores,
        ArchitectureViolationIdentity[] Findings,
        int RequiredMatches,
        int WildcardCount);

    private sealed record MatchResult(bool[] Suppressed, bool[] MatchedByLoadedBaseline, int[] MatchedIndexes);

    private sealed record Measurement(double ElapsedMilliseconds, long AllocatedBytes, string ResultChecksum);
}
