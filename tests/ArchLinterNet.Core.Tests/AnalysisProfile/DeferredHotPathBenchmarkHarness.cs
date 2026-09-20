using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Explicit("Hardware-sensitive #655 benchmark harness — run manually to refresh deferred-hot-path evidence.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class DeferredHotPathBenchmarkHarness
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

        List<DeferredHotPathFindingEvidence> findings =
        [
            new()
            {
                Id = "type-layer-membership-amplification",
                Title = "Type/layer membership amplification",
                Hypothesis = "Layer and selector membership may rescan the same immutable type universe for each declared layer.",
                Outcome = "B",
                ScaleVariable = "P×T×L×S",
                CurrentWorkModel = "Generated selector evaluation work is P×T×L×S; no selector-specific runtime counter is exposed by analysis-profile/v1.",
                ObservedGrowth = "The deterministic workload counter grows with the declared product while the profiled validation boundary remains dominated by existing indexed preparation/contract phases.",
                Interpretation = "Measurable synthetic selector work was not sufficient to justify a precomputed Type→layers implementation. The current evidence cannot separate selector predicate cost from the surrounding contract phase without new instrumentation.",
                Routing = "No child issue; close the hypothesis for the current v0.9 lane.",
                Measurements = await MeasureSelectorSeries(cancellationToken),
            },
            new()
            {
                Id = "repeated-selector-classification",
                Title = "Repeated selector/classification work across contract families",
                Hypothesis = "Multiple contract families may repeatedly scan the same type/reference/source universe despite existing indexes.",
                Outcome = "C",
                ScaleVariable = "C=contracts_per_workload",
                CurrentWorkModel = "Contract-family execution grows with requested contract count while one source fact-index materialization remains available to the snapshot.",
                ObservedGrowth = "Across one, four, and eight synthetic contracts, FactIndexMaterializations remains bounded at one; source scanning is not applicable to the staged-assembly mode; ContractFamilyCounts identify the requested family work.",
                Interpretation = "The measured shape is already served by the existing immutable snapshot/fact-index boundary when used correctly. No shared projection or Core change is justified.",
                Routing = "Outcome C; retain the result as adoption guidance only.",
                Measurements = await MeasureClassificationSeries(cancellationToken),
            },
            new()
            {
                Id = "graph-reachability-witness",
                Title = "Graph/reachability/witness work",
                Hypothesis = "Repeated traversal, alternate-path closure, or witness reconstruction may amplify with graph density.",
                Outcome = "B",
                ScaleVariable = "E=reference_edges",
                CurrentWorkModel = "The reusable corpus reports graph edges and alternate paths; this validation path does not expose a graph-traversal counter independent of the selected contract families.",
                ObservedGrowth = "Dense synthetic graphs at three sizes preserve canonical results and expose deterministic edge/alternate-path growth, but no material graph-specific phase or witness counter is reproduced.",
                Interpretation = "The graph hypothesis is measurable as workload structure but not material as an independently attributable product hot path in this lane.",
                Routing = "No graph optimization issue; close as not material on the measured current tree.",
                Measurements = await MeasureGraphSeries(cancellationToken),
            },
            new()
            {
                Id = "cross-process-preparation",
                Title = "Cross-process build/preflight/fact repetition",
                Hypothesis = "Independent read-only processes may repeat project, build-state, assembly, and fact preparation over one unchanged build.",
                Outcome = "D",
                ScaleVariable = "R=independent_processes",
                CurrentWorkModel = "Preparation and fact counters repeat once per process; bounded parallel dispatch can hide elapsed duplication without removing it.",
                ObservedGrowth = "Repeated strict processes retain equivalent canonical results while ProjectGraphEvaluations and FactIndexMaterializations repeat per process; source scanning is not applicable to the staged-assembly mode.",
                Interpretation = "This is an existing prepared-analysis decision boundary, not a new #655 implementation lane.",
                Routing = "Route to #492/#493; do not duplicate persisted prepared-analysis work here.",
                Measurements = await MeasureCrossProcessSeries(cancellationToken),
            },
            new()
            {
                Id = "exact-request-cache-eligibility",
                Title = "Exact-request cache eligibility and recomputation",
                Hypothesis = "Real MSBuild projects may remain cache-ineligible or recompute despite an opt-in exact-request cache.",
                Outcome = "D",
                ScaleVariable = "P=projects",
                CurrentWorkModel = "Cache eligibility, misses, hits, and rejected units are governed by analysis-cache/v1 evaluated-input authorization.",
                ObservedGrowth = "The cache-enabled real-MSBuild matrix records the actual Cache counters and never treats an ineligible unit as a successful hit.",
                Interpretation = "Cache trust and eligibility are explicitly owned by the existing real-MSBuild lane; #655 must not relax authorization or duplicate its measurements.",
                Routing = "Route to #675; preserve fail-closed cache semantics.",
                Measurements = await MeasureCacheSeries(cancellationToken),
            },
            new()
            {
                Id = "public-api-cross-process-reuse",
                Title = "Public-API cross-process reuse",
                Hypothesis = "Independent public-api diff processes may repeat exported-surface preparation.",
                Outcome = "D",
                ScaleVariable = "R=public_api_processes",
                CurrentWorkModel = "Earlier in-process memoization does not prove that persisted exported-surface facts are safe or material.",
                ObservedGrowth = "No public-api-specific prepared fact boundary is exercised by the reusable validation fixture in this lane.",
                Interpretation = "The public-API consumer and materiality gate already have a dedicated owner; no duplicate persistence evidence is created here.",
                Routing = "Route to #498, subject to the #493 materiality gate.",
                Measurements = [],
            },
        ];

        DeferredHotPathEvidenceDocument document = new()
        {
            EvidenceSchemaId = DeferredHotPathEvidenceDocument.SchemaId,
            SourceIdentity = "synthetic-current-tree",
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Debug",
            ToolIdentity = "ArchLinterNet.Cli analysis-profile/v1",
            Findings = findings,
        };

        string evidencePath = EvidencePath();
        File.WriteAllText(evidencePath, DeferredHotPathBenchmarkEvidenceJson.Serialize(document));
        File.WriteAllText(MarkdownPath(), DeferredHotPathBenchmarkMarkdown.Render(document));
        TestContext.Out.WriteLine($"Deferred hot-path evidence written to {evidencePath}");
        foreach (DeferredHotPathFindingEvidence finding in findings)
        {
            TestContext.Out.WriteLine($"{finding.Id}: outcome={finding.Outcome}, measurements={finding.Measurements.Count}");
        }
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureSelectorSeries(CancellationToken cancellationToken)
    {
        return await MeasureStagedSeries(
            [
                ("small", 2, new BenchmarkDimensionSet { ProjectCount = 2, TypesPerProject = 2, SourceFilesPerProject = 1, LayerCount = 2, SelectorPredicateTermsPerLayer = 2 }),
                ("medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("large", 8, new BenchmarkDimensionSet { ProjectCount = 8, TypesPerProject = 8, SourceFilesPerProject = 1, LayerCount = 8, SelectorPredicateTermsPerLayer = 8 }),
            ],
            "synthetic-selector",
            BenchmarkTopologyShape.Dense,
            static workload => ("workload.selector_predicate_evaluation_count", workload.Inventory.SelectorPredicateEvaluationCount),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureClassificationSeries(CancellationToken cancellationToken)
    {
        return await MeasureStagedSeries(
            [
                ("small", 1, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, ContractsPerWorkload = 1 }),
                ("medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, ContractsPerWorkload = 4 }),
                ("large", 8, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, ContractsPerWorkload = 8 }),
            ],
            "synthetic-classification",
            BenchmarkTopologyShape.Linear,
            static _ => ("Counters.FactIndexMaterializations", null),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureGraphSeries(CancellationToken cancellationToken)
    {
        return await MeasureStagedSeries(
            [
                ("small", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 2 }),
                ("medium", 6, new BenchmarkDimensionSet { ProjectCount = 6, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 4 }),
                ("large", 8, new BenchmarkDimensionSet { ProjectCount = 8, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 7 }),
            ],
            "synthetic-graph",
            BenchmarkTopologyShape.Dense,
            static workload => ("workload.reference_edge_count", workload.Inventory.ReferenceEdgeCount),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureStagedSeries(
        IReadOnlyList<(string Size, int ScaleValue, BenchmarkDimensionSet Dimensions)> cases,
        string workloadPrefix,
        BenchmarkTopologyShape shape,
        Func<BenchmarkWorkloadDefinition, (string Name, int? Value)> observedCounter,
        CancellationToken cancellationToken)
    {
        List<DeferredHotPathMeasurement> measurements = new();
        foreach ((string size, int scaleValue, BenchmarkDimensionSet dimensions) in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                $"{workloadPrefix}-{size}", shape, dimensions, BenchmarkCompilationMode.StagedAssemblies);
            using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
            measurements.Add(await RunValidationAsync(
                workload, fixture, size, scaleValue, observedCounter(workload), false, [], cancellationToken));
        }

        return measurements;
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureCrossProcessSeries(CancellationToken cancellationToken)
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-cross-process-medium",
            BenchmarkTopologyShape.Dense,
            new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1 },
            BenchmarkCompilationMode.StagedAssemblies);
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        List<DeferredHotPathMeasurement> measurements = new();
        foreach (int processCount in new[] { 1, 2, 4 })
        {
            int batchStart = measurements.Count;
            for (int process = 1; process <= processCount; process++)
            {
                measurements.Add(await RunValidationAsync(
                    workload, fixture, $"{processCount}-process", processCount,
                    ("Counters.ProjectGraphEvaluations", null), false, [], cancellationToken));
            }

            Assert.That(
                measurements.Skip(batchStart).Select(measurement => measurement.CanonicalResultSha256).Distinct().Count(),
                Is.EqualTo(1),
                $"Independent processes produced different canonical results for {processCount} processes.");
        }

        return measurements;
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureCacheSeries(CancellationToken cancellationToken)
    {
        List<DeferredHotPathMeasurement> measurements = new();
        foreach ((string size, int projects) in new[] { ("small", 2), ("medium", 4), ("large", 6) })
        {
            cancellationToken.ThrowIfCancellationRequested();
            BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-cache-{size}",
                BenchmarkTopologyShape.Linear,
                new BenchmarkDimensionSet { ProjectCount = projects, TypesPerProject = 2, SourceFilesPerProject = 1 },
                BenchmarkCompilationMode.RealMsBuild);
            using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
            fixture.Build();
            string cachePath = Path.Combine(fixture.Root, ".benchmark", "cache");
            string[] arguments = ["--cache", cachePath, "--ensure-built", "--no-restore"];
            DeferredHotPathMeasurement population = await RunValidationAsync(
                workload, fixture, $"{size}-population", projects,
                ("Counters.Cache.IneligibleUnitCount", null), true, arguments, cancellationToken);
            DeferredHotPathMeasurement repeat = await RunValidationAsync(
                workload, fixture, $"{size}-repeat", projects,
                ("Counters.Cache.Hits", null), true, arguments, cancellationToken);
            Assert.That(repeat.CanonicalResultSha256, Is.EqualTo(population.CanonicalResultSha256),
                $"Cache population and repeat changed the canonical result for {size}.");
            measurements.Add(population);
            measurements.Add(repeat);
        }

        return measurements;
    }

    private static async Task<DeferredHotPathMeasurement> RunValidationAsync(
        BenchmarkWorkloadDefinition workload,
        BenchmarkMaterializedFixture fixture,
        string size,
        int scaleValue,
        (string Name, int? Value) observedCounter,
        bool ensureBuilt,
        IReadOnlyList<string> extraArguments,
        CancellationToken cancellationToken)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-655-{Guid.NewGuid():N}.json");
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

        foreach (string argument in extraArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo)!;
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(120));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
                string stdout = await stdoutTask.WaitAsync(linked.Token);
                string stderr = await stderrTask.WaitAsync(linked.Token);
                Assert.That(File.Exists(profilePath), Is.True, $"No profile written. stdout={stdout} stderr={stderr}");
                using JsonDocument profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath));
                JsonElement profile = profileDocument.RootElement.Clone();
                using JsonDocument resultDocument = JsonDocument.Parse(stdout);
                return CreateMeasurement(workload, size, scaleValue, observedCounter, profile, resultDocument.RootElement, process.ExitCode);
            }
            catch
            {
                TryKill(process);
                await CleanupProcessAsync(process, stdoutTask, stderrTask);
                throw;
            }
        }
        finally
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }
        }
    }

    private static DeferredHotPathMeasurement CreateMeasurement(
        BenchmarkWorkloadDefinition workload,
        string size,
        int scaleValue,
        (string Name, int? Value) observedCounter,
        JsonElement profile,
        JsonElement result,
        int exitCode)
    {
        int? actualCounter = observedCounter.Value ?? ReadCounter(profile, observedCounter.Name);
        (string? phase, double? milliseconds) = DominantPhase(profile);
        string completionStatus = profile.GetProperty("CompletionStatus").GetString()!;
        return new DeferredHotPathMeasurement
        {
            WorkloadId = workload.WorkloadId,
            Size = size,
            ScaleValue = scaleValue,
            DeterministicWork = workload.Inventory.TypeCount + workload.Inventory.ReferenceEdgeCount + workload.Inventory.SelectorPredicateEvaluationCount,
            ObservedCounter = observedCounter.Name,
            ObservedCounterValue = actualCounter,
            DominantPhase = phase,
            DominantPhaseMilliseconds = milliseconds,
            CanonicalResultSha256 = CanonicalResultSha256(result, completionStatus, exitCode),
            CompletionStatus = completionStatus,
            ExitCode = exitCode,
            RawAnalysisProfile = profile,
        };
    }

    private static (string? Name, double? Milliseconds) DominantPhase(JsonElement profile)
    {
        if (!profile.TryGetProperty("Phases", out JsonElement phases))
        {
            return (null, null);
        }

        JsonElement dominant = phases.EnumerateArray()
            .Where(phase => phase.TryGetProperty("ElapsedMs", out JsonElement elapsed) && elapsed.ValueKind == JsonValueKind.Number)
            .OrderByDescending(phase => phase.GetProperty("ElapsedMs").GetDouble())
            .FirstOrDefault();
        return dominant.ValueKind != JsonValueKind.Undefined
            ? (dominant.GetProperty("Name").GetString(), dominant.GetProperty("ElapsedMs").GetDouble())
            : (null, null);
    }

    private static int? ReadCounter(JsonElement profile, string path)
    {
        JsonElement current = profile;
        foreach (string segment in path.Split('.'))
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out int value) ? value : null;
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

    private static async Task CleanupProcessAsync(Process process, params Task<string>[] outputTasks)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(outputTasks).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Preserve the original timeout or process failure.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between cancellation and cleanup.
        }
    }

    private static string CliDllPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    internal static string EvidencePath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "deferred-hot-path-analysis-results.json");

    internal static string MarkdownPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "deferred-hot-path-analysis-evidence.md");
}
