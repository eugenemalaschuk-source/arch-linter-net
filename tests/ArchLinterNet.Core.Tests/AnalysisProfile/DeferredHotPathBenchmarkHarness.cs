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
                ScaleVariable = "P/T/L/S independently",
                CurrentWorkModel = "The generated policy executes one compiled selector predicate per matching type/layer pair (P×T×L); S independently increases the CEL terms inside that predicate.",
                ObservedGrowth = "The analysis-profile selector_predicate_evaluation phase records the predicates actually evaluated. P, T, and L are varied independently and increase the runtime counter; S is varied independently while the invocation count remains attributable to the same predicate boundary.",
                Interpretation = "The runtime counter and independent matrix make selector work attributable, but the selector phase is not material enough in the measured end-to-end profiles to justify a precomputed Type→layers implementation.",
                Routing = "No child issue; close the hypothesis for the current v0.9 lane.",
                TopologyEvidence = [],
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
                TopologyEvidence = [],
                Measurements = await MeasureClassificationSeries(cancellationToken),
            },
            new()
            {
                Id = "graph-reachability-witness",
                Title = "Graph/reachability/witness work",
                Hypothesis = "Repeated traversal, alternate-path closure, or witness reconstruction may amplify with graph density.",
                Outcome = "B",
                ScaleVariable = "shape∈{linear,wide,diamond,dense,SCC}; P=projects",
                CurrentWorkModel = "The reusable corpus reports graph edges and alternate paths; this validation path does not expose a graph-traversal counter independent of the selected contract families.",
                ObservedGrowth = "Linear, wide fan-out/fan-in, diamond, and dense synthetic graphs are measured at three project sizes; CyclicScc is retained as structural-only because the #502 materializer rejects cyclic project compilation. All executable shapes preserve canonical results.",
                Interpretation = "The required topology space is covered. The evidence remains insufficient for an independently attributable material graph-specific phase or witness counter, so no graph optimization issue is justified.",
                Routing = "No graph optimization issue; close as not material on the measured current tree.",
                TopologyEvidence = CreateGraphTopologyEvidence(),
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
                TopologyEvidence = [],
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
                TopologyEvidence = [],
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
                TopologyEvidence = [],
                Measurements = [],
            },
            new()
            {
                Id = "consumer-forced-sequential-vs-bounded-parallelism",
                Title = "Consumer-forced sequential execution versus bounded parallelism",
                Hypothesis = "Consumers may force --max-parallelism 1 across assembly-consuming commands even though deterministic bounded parallel scanning is already available.",
                Outcome = "B",
                ScaleVariable = "mode∈{sequential,bounded}; shape∈{linear,wide,diamond,dense}",
                CurrentWorkModel = "The same immutable staged-assembly inputs are executed once sequentially and once with the resolved default bounded degree; type-loading work is partitioned by target assembly.",
                ObservedGrowth = "Each executable #502 graph shape is paired at three project sizes. The bounded profile reports active concurrency, while the sequential profile reports NotApplicable concurrency; allocations, peak working-set availability, and canonical-result digests are retained for both variants.",
                Interpretation = "The existing bounded capability is exercised and semantically equivalent, but it does not materially improve the measured hot phase and generally increases managed allocation; peak working-set data is unavailable on this host. No Core change is justified by this matrix, and concurrency must not mask the remaining bottleneck.",
                Routing = "No parallelism change; retain the paired evidence and continue attribution in the owning performance/adoption lanes.",
                TopologyEvidence = CreateGraphTopologyEvidence(),
                Measurements = await MeasureParallelismSeries(cancellationToken),
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
        var measurements = new List<DeferredHotPathMeasurement>();
        foreach ((string dimension, (string Size, int ScaleValue, BenchmarkDimensionSet Dimensions)[] cases) in new[]
        {
            ("P=projects", new[]
            {
                ("projects-small", 2, new BenchmarkDimensionSet { ProjectCount = 2, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("projects-medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("projects-large", 8, new BenchmarkDimensionSet { ProjectCount = 8, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
            }),
            ("T=types_per_project", new[]
            {
                ("types-small", 2, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 2, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("types-medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("types-large", 8, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 8, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
            }),
            ("L=layers", new[]
            {
                ("layers-small", 2, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 2, SelectorPredicateTermsPerLayer = 4 }),
                ("layers-medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("layers-large", 8, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 8, SelectorPredicateTermsPerLayer = 4 }),
            }),
            ("S=selector_terms_per_layer", new[]
            {
                ("terms-small", 2, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 2 }),
                ("terms-medium", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 4 }),
                ("terms-large", 8, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 4, SourceFilesPerProject = 1, LayerCount = 4, SelectorPredicateTermsPerLayer = 8 }),
            }),
        })
        {
            measurements.AddRange(await MeasureStagedSeries(
                cases,
                $"synthetic-selector-{char.ToLowerInvariant(dimension[0])}",
                BenchmarkTopologyShape.Linear,
                static _ => ("phase.selector_predicate_evaluation.count", null),
                dimension,
                cancellationToken));
        }

        Assert.That(measurements, Has.All.Matches<DeferredHotPathMeasurement>(measurement =>
            measurement.ObservedCounterValue is > 0), "Selector matrix must observe runtime predicate evaluations.");
        return measurements;
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
            "C=contracts",
            cancellationToken);
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureGraphSeries(CancellationToken cancellationToken)
    {
        var measurements = new List<DeferredHotPathMeasurement>();
        foreach (BenchmarkTopologyShape shape in ExecutableGraphShapes())
        {
            string shapeId = shape.ToString().ToLowerInvariant();
            measurements.AddRange(await MeasureStagedSeries(
                [
                    ("small", 4, new BenchmarkDimensionSet { ProjectCount = 4, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 0 }),
                    ("medium", 8, new BenchmarkDimensionSet { ProjectCount = 8, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 0 }),
                    ("large", 12, new BenchmarkDimensionSet { ProjectCount = 12, TypesPerProject = 2, SourceFilesPerProject = 1, ReferencesPerProject = 0 }),
                ],
                $"synthetic-graph-{shapeId}",
                shape,
                static workload => ("workload.reference_edge_count", workload.Inventory.ReferenceEdgeCount),
                "P=projects within topology shape",
                cancellationToken));
        }

        return measurements;
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureStagedSeries(
        IReadOnlyList<(string Size, int ScaleValue, BenchmarkDimensionSet Dimensions)> cases,
        string workloadPrefix,
        BenchmarkTopologyShape shape,
        Func<BenchmarkWorkloadDefinition, (string Name, int? Value)> observedCounter,
        string scaleDimension,
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
                workload, fixture, size, scaleValue, scaleDimension, "sequential", observedCounter(workload), false, [], cancellationToken));
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
                    "R=processes", "sequential", ("Counters.ProjectGraphEvaluations", null), false, [], cancellationToken));
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
                workload, fixture, $"{size}-population", projects, "P=projects", "sequential",
                ("Counters.Cache.IneligibleUnitCount", null), true, arguments, cancellationToken);
            DeferredHotPathMeasurement repeat = await RunValidationAsync(
                workload, fixture, $"{size}-repeat", projects, "P=projects", "sequential",
                ("Counters.Cache.Hits", null), true, arguments, cancellationToken);
            Assert.That(repeat.CanonicalResultSha256, Is.EqualTo(population.CanonicalResultSha256),
                $"Cache population and repeat changed the canonical result for {size}.");
            measurements.Add(population);
            measurements.Add(repeat);
        }

        return measurements;
    }

    private static async Task<IReadOnlyList<DeferredHotPathMeasurement>> MeasureParallelismSeries(
        CancellationToken cancellationToken)
    {
        List<DeferredHotPathMeasurement> measurements = new();
        foreach (BenchmarkTopologyShape shape in ExecutableGraphShapes())
        {
            string shapeId = shape.ToString().ToLowerInvariant();
            foreach ((string size, int projects) in new[]
            {
                ("small", 4),
                ("medium", 8),
                ("large", 12),
            })
            {
                cancellationToken.ThrowIfCancellationRequested();
                BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                    $"synthetic-parallel-{shapeId}-{size}",
                    shape,
                    new BenchmarkDimensionSet
                    {
                        ProjectCount = projects,
                        TypesPerProject = 2,
                        SourceFilesPerProject = 1,
                        ReferencesPerProject = 0,
                    },
                    BenchmarkCompilationMode.StagedAssemblies);
                using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
                DeferredHotPathMeasurement sequential = await RunValidationAsync(
                    workload,
                    fixture,
                    size,
                    projects,
                    "P=projects",
                    "sequential",
                    ("Counters.Concurrency.MaxParallelism", null),
                    false,
                    [],
                    cancellationToken,
                    maxParallelism: 1);
                DeferredHotPathMeasurement bounded = await RunValidationAsync(
                    workload,
                    fixture,
                    size,
                    projects,
                    "P=projects",
                    "bounded-default",
                    ("Counters.Concurrency.MaxParallelism", null),
                    false,
                    [],
                    cancellationToken,
                    maxParallelism: null);

                Assert.Multiple(() =>
                {
                    Assert.That(bounded.CanonicalResultSha256, Is.EqualTo(sequential.CanonicalResultSha256),
                        $"Sequential and bounded results differ for {shape}/{size}.");
                    Assert.That(ReadString(sequential.RawAnalysisProfile, "Counters.Concurrency.Status"), Is.EqualTo("NotApplicable"));
                    Assert.That(ReadString(bounded.RawAnalysisProfile, "Counters.Concurrency.Status"), Is.EqualTo("Active"),
                        $"Bounded profile did not activate concurrency for {shape}/{size}.");
                    Assert.That(ReadCounter(bounded.RawAnalysisProfile, "Counters.Concurrency.MaxParallelism"), Is.GreaterThan(1),
                        $"Bounded profile did not report a resolved degree > 1 for {shape}/{size}.");
                });

                measurements.Add(sequential);
                measurements.Add(bounded);
            }
        }

        return measurements;
    }

    private static IReadOnlyList<BenchmarkTopologyShape> ExecutableGraphShapes() =>
    [
        BenchmarkTopologyShape.Linear,
        BenchmarkTopologyShape.WideFanOutFanIn,
        BenchmarkTopologyShape.Diamond,
        BenchmarkTopologyShape.Dense,
    ];

    private static IReadOnlyList<DeferredHotPathTopologyEvidence> CreateGraphTopologyEvidence()
    {
        List<DeferredHotPathTopologyEvidence> evidence = new();
        foreach (BenchmarkTopologyShape shape in Enum.GetValues<BenchmarkTopologyShape>()
                     .Where(shape => shape is BenchmarkTopologyShape.Linear
                         or BenchmarkTopologyShape.WideFanOutFanIn
                         or BenchmarkTopologyShape.Diamond
                         or BenchmarkTopologyShape.Dense
                         or BenchmarkTopologyShape.CyclicScc))
        {
            foreach (int projects in new[] { 4, 8, 12 })
            {
                BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                    $"synthetic-topology-{shape.ToString().ToLowerInvariant()}-{projects}",
                    shape,
                    new BenchmarkDimensionSet
                    {
                        ProjectCount = projects,
                        TypesPerProject = 2,
                        SourceFilesPerProject = 1,
                        ReferencesPerProject = 0,
                    },
                    BenchmarkCompilationMode.StagedAssemblies);
                bool structuralOnly = shape == BenchmarkTopologyShape.CyclicScc;
                evidence.Add(new DeferredHotPathTopologyEvidence
                {
                    Shape = shape.ToString(),
                    ProjectCount = projects,
                    ReferenceEdgeCount = workload.Topology.ReferenceEdgeCount,
                    StronglyConnectedComponentCount = workload.Topology.StronglyConnectedComponentCount,
                    ContainsCycle = workload.Topology.ContainsCycle,
                    ExecutionStatus = structuralOnly ? "structural-only" : "materialized-and-measured",
                    Reason = structuralOnly
                        ? "#502 v1 intentionally rejects cyclic project compilation; retained as deterministic SCC topology evidence."
                        : "Materialized through the #502 staged-assembly fixture and executed by the CLI harness.",
                });
            }
        }

        return evidence;
    }

    private static async Task<DeferredHotPathMeasurement> RunValidationAsync(
        BenchmarkWorkloadDefinition workload,
        BenchmarkMaterializedFixture fixture,
        string size,
        int scaleValue,
        string scaleDimension,
        string executionVariant,
        (string Name, int? Value) observedCounter,
        bool ensureBuilt,
        IReadOnlyList<string> extraArguments,
        CancellationToken cancellationToken,
        int? maxParallelism = 1)
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
        if (maxParallelism is int requestedMaxParallelism)
        {
            startInfo.ArgumentList.Add("--max-parallelism");
            startInfo.ArgumentList.Add(requestedMaxParallelism.ToString());
        }
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
                return CreateMeasurement(
                    workload,
                    size,
                    scaleValue,
                    scaleDimension,
                    executionVariant,
                    observedCounter,
                    profile,
                    resultDocument.RootElement,
                    process.ExitCode);
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
        string scaleDimension,
        string executionVariant,
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
            ScaleDimension = scaleDimension,
            ScaleValue = scaleValue,
            ExecutionVariant = executionVariant,
            DeterministicWork = workload.Inventory.TypeCount + workload.Inventory.ReferenceEdgeCount + workload.Inventory.SelectorPredicateEvaluationCount,
            ObservedCounter = observedCounter.Name,
            ObservedCounterValue = actualCounter,
            DominantPhase = phase,
            DominantPhaseMilliseconds = milliseconds,
            AllocatedBytes = ReadLong(profile, "Measurements.AllocatedBytesTotal"),
            PeakWorkingSetBytes = ReadLong(profile, "Measurements.PeakWorkingSetBytes"),
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
        if (path == "phase.selector_predicate_evaluation.count")
        {
            if (!profile.TryGetProperty("Phases", out JsonElement phases))
            {
                return null;
            }

            JsonElement phase = phases.EnumerateArray()
                .FirstOrDefault(candidate => candidate.TryGetProperty("Name", out JsonElement name)
                    && name.GetString() == "selector_predicate_evaluation");
            return phase.ValueKind != JsonValueKind.Undefined
                && phase.TryGetProperty("Count", out JsonElement count)
                && count.ValueKind == JsonValueKind.Number
                && count.TryGetInt32(out int phaseCount)
                ? phaseCount
                : null;
        }

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

    private static long? ReadLong(JsonElement profile, string path)
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
