using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Explicit("Hardware-sensitive #675 real-MSBuild cache eligibility evidence; run manually to refresh evidence.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class RealMsBuildCacheEligibilityBenchmarkHarness
{
    private static readonly string[] _canonicalResultFields =
    [
        "passed", "mode", "violations", "cycles", "cycle_diagnostics", "coverage_findings",
        "unmatched_ignored_violations", "policy_consistency_findings", "coverage_summary",
        "classification_conflicts", "classification_metadata_failures", "classification_roles",
        "classification_path_deferred",
    ];

    [Test]
    public async Task RunBenchmarkMatrix()
    {
        string cliPath = CliDllPath();
        Assert.That(File.Exists(cliPath), Is.True, $"CLI not built at {cliPath} — run dotnet build first.");
        CancellationToken cancellationToken = TestContext.CurrentContext.CancellationToken;
        List<RealMsBuildCacheMeasurement> measurements = [];

        foreach ((string size, int projects) in new[] { ("small", 2), ("medium", 4), ("large", 6) })
        {
            cancellationToken.ThrowIfCancellationRequested();
            BenchmarkWorkloadDefinition realWorkload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-real-msbuild-cache-{size}",
                BenchmarkTopologyShape.Linear,
                new BenchmarkDimensionSet
                {
                    ProjectCount = projects,
                    TypesPerProject = 4,
                    SourceFilesPerProject = 2,
                    ReferencesPerProject = 1,
                },
                BenchmarkCompilationMode.RealMsBuild);
            using BenchmarkMaterializedFixture realFixture = BenchmarkFixtureMaterializer.Materialize(realWorkload);
            AddOrdinaryMsBuildFrameworkReference(realFixture);
            realFixture.Build();
            IReadOnlyList<string> realReasons = CollectIneligibilityReasons(realFixture);
            string cachePath = Path.Combine(realFixture.Root, ".benchmark", "analysis-cache");
            RealMsBuildCacheMeasurement disabled = await RunObservationAsync(realWorkload, realFixture, size, "disabled", null, true, realReasons, cancellationToken);
            realReasons = CollectReceiptReasons(realFixture);
            if (realReasons.Count == 0)
            {
                string[] receiptFiles = Directory.GetFiles(realFixture.Root, "*.archlinternet-receipt.json", SearchOption.AllDirectories);
                TestContext.Out.WriteLine($"No typed receipt reasons for {size}; receipt files: {string.Join(", ", receiptFiles)}");
                realReasons = ["receipt-reason-not-published"];
            }
            measurements.Add(disabled with { IneligibilityReasons = realReasons });
            measurements.Add(await RunObservationAsync(realWorkload, realFixture, size, "population", cachePath, true, realReasons, cancellationToken));
            measurements.Add(await RunObservationAsync(realWorkload, realFixture, size, "repeat", cachePath, true, realReasons, cancellationToken));

            BenchmarkWorkloadDefinition eligibleWorkload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-eligible-control-cache-{size}",
                BenchmarkTopologyShape.Linear,
                new BenchmarkDimensionSet
                {
                    ProjectCount = projects,
                    TypesPerProject = 4,
                    SourceFilesPerProject = 2,
                    ReferencesPerProject = 1,
                },
                BenchmarkCompilationMode.StagedAssemblies);
            using BenchmarkMaterializedFixture eligibleFixture = BenchmarkFixtureMaterializer.Materialize(eligibleWorkload);
            PromoteStagedReceiptsToVerifiedEligibility(eligibleFixture);
            string eligibleCachePath = Path.Combine(eligibleFixture.Root, ".benchmark", "analysis-cache");
            measurements.Add(await RunObservationAsync(eligibleWorkload, eligibleFixture, size, "disabled", null, false, [], cancellationToken));
            measurements.Add(await RunObservationAsync(eligibleWorkload, eligibleFixture, size, "population", eligibleCachePath, false, [], cancellationToken));
            measurements.Add(await RunObservationAsync(eligibleWorkload, eligibleFixture, size, "repeat", eligibleCachePath, false, [], cancellationToken));

            string[] canonicalResults = measurements
                .Where(measurement => measurement.Size == size)
                .Select(measurement => measurement.CanonicalResultSha256)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.That(canonicalResults, Has.Length.EqualTo(1), $"Cache modes changed the canonical result for {size}.");
        }

        RealMsBuildCacheEffectEstimate effectEstimate = RealMsBuildCacheEffectModel.Calculate(measurements);
        RealMsBuildCacheEligibilityEvidenceDocument document = new()
        {
            EvidenceSchemaId = RealMsBuildCacheEligibilityEvidenceDocument.SchemaId,
            IssueReference = "#675",
            SourceIdentity = "synthetic-current-tree",
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Debug",
            ToolIdentity = "ArchLinterNet.Cli analysis-profile/v1",
            NormalizationGate = new RealMsBuildNormalizationGate
            {
                IssueReference = "#991",
                Status = "open",
                Phase2Authorized = false,
                Evidence = "#991 remains open; normalized consumer workflows must be remeasured before any Phase 2 eligibility implementation.",
            },
            Decision = "C",
            DecisionRationale = "The current ordinary real-MSBuild matrix remains CacheIneligible with zero verified exact-request hits. The separately labelled eligibility-control attempt does not produce a verified hit, so the measured targeted-phase share is only a conservative upper bound; the normalized #991 consumer gate is still open, and available evidence does not justify an eligibility expansion or a Phase 2 implementation.",
            ReferenceBaseDisposition = new RealMsBuildReferenceBaseDisposition
            {
                Disposition = "routed-to-owning-lane",
                CountedInEffectEstimate = false,
                Evidence = "The current exact-request cache boundary covers validation modes, while immutable reference/base preparation is a separate consumer/prepared-analysis concern; it is routed separately and never counted as candidate cache savings.",
            },
            EffectEstimate = effectEstimate,
            Measurements = measurements,
            StaleInputChecks = StaleInputChecks(),
            Phase2Routing = "Do not begin Phase 2 until #991 completes and the normalized dogfood workflows are remeasured; any future outcome A must be recorded only after that gate.",
        };
        document.Validate();

        string evidencePath = EvidencePath();
        File.WriteAllText(evidencePath, RealMsBuildCacheEligibilityEvidenceSerialization.Serialize(document));
        File.WriteAllText(MarkdownPath(), RealMsBuildCacheEligibilityEvidenceMarkdown.Render(document));
        TestContext.Out.WriteLine($"Real-MSBuild cache eligibility evidence written to {evidencePath}");
        TestContext.Out.WriteLine($"Phase 1 outcome: {document.Decision}; real-MSBuild hits: {measurements.Where(measurement => measurement.FixtureKind == "real-msbuild").Sum(measurement => measurement.Hits)}");
    }

    private static async Task<RealMsBuildCacheMeasurement> RunObservationAsync(
        BenchmarkWorkloadDefinition workload,
        BenchmarkMaterializedFixture fixture,
        string size,
        string cacheMode,
        string? cachePath,
        bool ensureBuilt,
        IReadOnlyList<string> ineligibilityReasons,
        CancellationToken cancellationToken)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-675-{Guid.NewGuid():N}.json");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
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
        startInfo.ArgumentList.Add("--max-parallelism");
        startInfo.ArgumentList.Add("1");
        if (ensureBuilt)
        {
            startInfo.ArgumentList.Add("--ensure-built");
        }

        startInfo.ArgumentList.Add("--no-restore");
        if (cachePath != null)
        {
            startInfo.ArgumentList.Add("--cache");
            startInfo.ArgumentList.Add(cachePath);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            using Process process = Process.Start(startInfo)!;
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(180));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await process.WaitForExitAsync(linked.Token);
            string stdout = await stdoutTask.WaitAsync(linked.Token);
            string stderr = await stderrTask.WaitAsync(linked.Token);
            stopwatch.Stop();
            Assert.That(process.ExitCode, Is.EqualTo(0), $"Benchmark command failed for {workload.WorkloadId}/{cacheMode}. stdout={stdout} stderr={stderr}");
            Assert.That(File.Exists(profilePath), Is.True, $"No profile written for {workload.WorkloadId}/{cacheMode}.");
            using JsonDocument profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath));
            using JsonDocument resultDocument = JsonDocument.Parse(stdout);
            JsonElement profile = profileDocument.RootElement.Clone();
            JsonElement result = resultDocument.RootElement.Clone();
            bool isReal = workload.CompilationMode == BenchmarkCompilationMode.RealMsBuild;
            long hits = ReadCounter(profile, "Counters.Cache.Hits");
            return new RealMsBuildCacheMeasurement
            {
                FixtureKind = isReal ? "real-msbuild" : "eligible-control",
                CacheMode = cacheMode,
                WorkloadId = workload.WorkloadId,
                WorkloadIdentity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(workload.WorkloadId))),
                Size = size,
                ProjectCount = workload.Dimensions.ProjectCount,
                Eligibility = isReal ? "CacheIneligible" : (hits > 0 ? "VerifiedCacheEligible" : "ControlUnavailable"),
                IneligibilityReasons = ineligibilityReasons.OrderBy(reason => reason, StringComparer.Ordinal).ToArray(),
                Lookups = ReadCounter(profile, "Counters.Cache.Lookups"),
                Hits = hits,
                Misses = ReadCounter(profile, "Counters.Cache.Misses"),
                Rejects = ReadCounter(profile, "Counters.Cache.Rejects"),
                Writes = ReadCounter(profile, "Counters.Cache.Writes"),
                IneligibleUnitCount = ReadCounter(profile, "Counters.Cache.IneligibleUnitCount"),
                BytesRead = ReadOptionalCounter(profile, "Counters.Cache.BytesRead"),
                BytesWritten = ReadOptionalCounter(profile, "Counters.Cache.BytesWritten"),
                AvoidedWork = ReadOptionalCounter(profile, "Counters.Cache.AvoidedFactIndexMaterializations") +
                    ReadOptionalCounter(profile, "Counters.Cache.AvoidedContractExecutions"),
                DeterministicWork = ReadCounter(profile, "Counters.ProjectGraphEvaluations") +
                    ReadCounter(profile, "Counters.AssemblyLoads") +
                    ReadCounter(profile, "Counters.FactIndexMaterializations") +
                    ReadCounter(profile, "Counters.ContractExecutions"),
                TotalElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                TargetedPhaseMilliseconds = ReadTargetedPhaseMilliseconds(profile),
                TargetedPhaseSharePercent = TargetedPhaseShare(profile, stopwatch.Elapsed.TotalMilliseconds),
                AllocatedBytes = ReadOptionalCounter(profile, "Measurements.AllocatedBytesTotal"),
                PeakWorkingSetBytes = ReadOptionalCounter(profile, "Measurements.PeakWorkingSetBytes"),
                CanonicalResultSha256 = CanonicalResultSha256(result, ReadString(profile, "CompletionStatus") ?? "unknown", process.ExitCode),
                CompletionStatus = ReadString(profile, "CompletionStatus") ?? "unknown",
                ExitCode = process.ExitCode,
            };
        }
        catch
        {
            stopwatch.Stop();
            throw;
        }
        finally
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }
        }
    }

    private static IReadOnlyList<string> CollectIneligibilityReasons(BenchmarkMaterializedFixture fixture)
    {
        SortedSet<string> reasons = new(StringComparer.Ordinal);
        foreach (BenchmarkProjectNode project in fixture.Definition.Projects)
        {
            string projectPath = Path.Combine(fixture.Root, "src", project.AssemblyName, $"{project.AssemblyName}.csproj");
            EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(projectPath, fixture.Root);
            foreach (string reason in manifest.IneligibilityReasons)
            {
                reasons.Add(reason);
            }
        }

        return reasons.ToArray();
    }

    private static void AddOrdinaryMsBuildFrameworkReference(BenchmarkMaterializedFixture fixture)
    {
        foreach (BenchmarkProjectNode project in fixture.Definition.Projects)
        {
            string projectPath = Path.Combine(fixture.Root, "src", project.AssemblyName, $"{project.AssemblyName}.csproj");
            string projectContents = File.ReadAllText(projectPath);
            int closingProjectIndex = projectContents.LastIndexOf("</Project>", StringComparison.Ordinal);
            Assert.That(closingProjectIndex, Is.GreaterThanOrEqualTo(0), $"Generated project is malformed: {projectPath}");
            string frameworkReference =
                "  <ItemGroup>" + Environment.NewLine +
                "    <FrameworkReference Include=\"Microsoft.AspNetCore.App\" />" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine;
            File.WriteAllText(projectPath, projectContents.Insert(closingProjectIndex, frameworkReference));
        }
    }

    private static IReadOnlyList<string> CollectReceiptReasons(BenchmarkMaterializedFixture fixture)
    {
        SortedSet<string> reasons = new(StringComparer.Ordinal);
        foreach (BenchmarkProjectNode project in fixture.Definition.Projects)
        {
            string assemblyPath = Path.Combine(fixture.Root, "src", project.AssemblyName, "bin", "Debug", "net10.0", $"{project.AssemblyName}.dll");
            if (BuildReceiptStore.TryRead(assemblyPath, out BuildReceiptV1? receipt) && receipt != null)
            {
                foreach (string reason in receipt.CacheIneligibilityReasons)
                {
                    reasons.Add(reason);
                }
            }
        }

        return reasons.ToArray();
    }

    private static void PromoteStagedReceiptsToVerifiedEligibility(BenchmarkMaterializedFixture fixture)
    {
        foreach (BenchmarkProjectNode project in fixture.Definition.Projects)
        {
            string projectPath = Path.Combine(fixture.Root, "src", project.AssemblyName, $"{project.AssemblyName}.csproj");
            string stagedAssemblyPath = Path.Combine(fixture.Root, ".benchmark", "staged-assemblies", $"{project.AssemblyName}.dll");
            EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(projectPath, fixture.Root);
            Assert.That(manifest.Eligibility, Is.EqualTo(CacheEligibility.VerifiedCacheEligible),
                $"The staged control must be eligible before it is used to calibrate a real-MSBuild hit.");
            Assert.That(BuildReceiptStore.TryRead(stagedAssemblyPath, out BuildReceiptV1? receipt), Is.True);
            Assert.That(receipt, Is.Not.Null);
            BuildReceiptStore.Write(
                stagedAssemblyPath,
                receipt! with
                {
                    EvaluatedManifestFingerprint = manifest.Digest,
                    CacheEligibility = manifest.Eligibility,
                    CacheIneligibilityReasons = manifest.IneligibilityReasons,
                });
        }
    }

    private static IReadOnlyList<RealMsBuildStaleInputCheck> StaleInputChecks() =>
    [
        new() { ChangeKind = "project", Disposition = "reject", Evidence = "Existing evaluated-manifest digest comparison rejects changed project/import inputs." },
        new() { ChangeKind = "source", Disposition = "reject-or-ineligible", Evidence = "Source inputs outside the exact manifest remain fail-closed; no timestamp-only authorization is used." },
        new() { ChangeKind = "package-or-configuration", Disposition = "reject", Evidence = "Package/framework/reference/configuration/build identity changes alter authorization or cache key." },
        new() { ChangeKind = "artifact", Disposition = "reject", Evidence = "Selected PE/PDB/build-receipt byte changes are covered by existing artifact-manifest rejection tests." },
    ];

    private static double? ReadTargetedPhaseMilliseconds(JsonElement profile)
    {
        if (!profile.TryGetProperty("Phases", out JsonElement phases))
        {
            return null;
        }

        JsonElement phase = phases.EnumerateArray()
            .Where(candidate => candidate.TryGetProperty("Name", out JsonElement name) &&
                name.GetString()?.Contains("contract", StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(candidate => candidate.TryGetProperty("ElapsedMs", out JsonElement elapsed) ? elapsed.GetDouble() : 0)
            .FirstOrDefault();
        return phase.ValueKind != JsonValueKind.Undefined && phase.TryGetProperty("ElapsedMs", out JsonElement value)
            ? value.GetDouble()
            : null;
    }

    private static double? TargetedPhaseShare(JsonElement profile, double totalMilliseconds)
    {
        double? targeted = ReadTargetedPhaseMilliseconds(profile);
        return targeted.HasValue && totalMilliseconds > 0 ? targeted.Value / totalMilliseconds * 100 : null;
    }

    private static long ReadCounter(JsonElement profile, string path) => ReadOptionalCounter(profile, path) ?? 0;

    private static long? ReadOptionalCounter(JsonElement profile, string path)
    {
        JsonElement current = profile;
        foreach (string segment in path.Split('.'))
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt64(out long value) ? value : null;
    }

    private static string? ReadString(JsonElement profile, string path)
    {
        JsonElement current = profile;
        foreach (string segment in path.Split('.'))
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string CanonicalResultSha256(JsonElement result, string completionStatus, int exitCode)
    {
        string canonical = string.Join(
            "\n",
            new[] { $"completion_status={completionStatus}", $"exit_code={exitCode}" }
                .Concat(_canonicalResultFields.Select(field =>
                    $"{field}={(result.TryGetProperty(field, out JsonElement value) ? value.GetRawText() : "null")}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CliDllPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    private static string EvidencePath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "real-msbuild-cache-eligibility-evidence.json");

    private static string MarkdownPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "real-msbuild-cache-eligibility-evidence.md");
}
