using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #493: explicit, hardware-sensitive evidence harness. It is intentionally excluded from
// make test/acceptance; the checked-in artifact is refreshed only by invoking this test directly.
[TestFixture]
[Explicit("Cross-process preparation reuse evidence harness; run manually for issue #493.")]
[Category("Benchmark")]
[CancelAfter(1_200_000)]
public sealed class PreparedAnalysisReuseBenchmarkHarness
{
    private const int ProcessTimeoutMilliseconds = 300_000;
    private const string EvidencePath = "docs/internal/prepared-analysis-reuse-evidence.json";

    [Test]
    public void MeasureCrossProcessPreparationReuse()
    {
        CancellationToken cancellationToken = TestContext.CurrentContext.CancellationToken;
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        Assert.That(File.Exists(CliDllPath(repositoryRoot)), Is.True,
            $"Release CLI not built at {CliDllPath(repositoryRoot)}; run `make pack` first.");

        List<CrossProcessPreparationEvidenceDocument> archetypes = [];
        foreach (BenchmarkCompilationMode compilationMode in Enum.GetValues<BenchmarkCompilationMode>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            archetypes.Add(MeasureArchetype(compilationMode, repositoryRoot, cancellationToken));
        }

        CrossProcessPreparationEvidenceBundle bundle = new()
        {
            EvidenceSchemaId = CrossProcessPreparationEvidenceBundle.SchemaId,
            Archetypes = archetypes,
        };
        string json = CrossProcessPreparationEvidenceBundleJson.Serialize(bundle);
        string path = Path.Combine(repositoryRoot, EvidencePath);
        File.WriteAllText(path, json + Environment.NewLine);
        TestContext.Out.WriteLine($"Issue #493 evidence written to {path}");
    }

    private static CrossProcessPreparationEvidenceDocument MeasureArchetype(
        BenchmarkCompilationMode compilationMode,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            compilationMode == BenchmarkCompilationMode.RealMsBuild
                ? "synthetic-cross-process-real-msbuild"
                : "synthetic-cross-process-staged-assemblies",
            BenchmarkTopologyShape.ManyProjectsFewTypes,
            new BenchmarkDimensionSet
            {
                ProjectCount = 1,
                TypesPerProject = 32,
                SourceFilesPerProject = 8,
                ReferencesPerProject = 0,
                LayerCount = 4,
                SelectorPredicateTermsPerLayer = 8,
                ContractsPerWorkload = 4,
                FindingCandidates = 0,
                SourceRootCount = 2,
            },
            compilationMode,
            BenchmarkExecutionMode.FullGovernance,
            independentProcesses: 10);

        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        cancellationToken.ThrowIfCancellationRequested();
        if (compilationMode == BenchmarkCompilationMode.RealMsBuild)
        {
            fixture.Build();
            WriteRealMsBuildReceipts(fixture);
        }

        string baselinePath = Path.Combine(fixture.Root, "empty-baseline.arch.yml");
        File.WriteAllText(baselinePath, "version: 3\nbaseline: {}\nmetric_baselines: []\n");
        string changePolicyPath = CreateChangeSnapshotPolicy(fixture, workload);

        List<CrossProcessProcessEvidence> processes = [];
        List<BenchmarkProfileSample> samples = [];
        int ordinal = 0;
        string candidateRevision = "synthetic-candidate-revision";
        string baseRevision = "synthetic-base-revision";

        CliObservation preparationPrime = RunCommand(
            fixture,
            "strict",
            "disabled",
            baselinePath,
            repositoryRoot,
            changePolicyPath,
            cancellationToken);
        RecordIndependentProcess(
            workload,
            "strict",
            "disabled",
            preparationPrime,
            "preparation-priming",
            processes,
            samples,
            ref ordinal);

        (string Family, string CacheMode)[] parallelCommands = [("strict", "disabled"), ("audit", "disabled")];
        Task<CliObservation>[] parallelTasks = parallelCommands
            .Select(command => Task.Run(() => RunCommand(
                fixture,
                command.Family,
                command.CacheMode,
                baselinePath,
                repositoryRoot,
                changePolicyPath,
                cancellationToken,
                ensureBuiltOverride: false)))
            .ToArray();
        for (int index = 0; index < parallelCommands.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordIndependentProcess(
                workload,
                parallelCommands[index].Family,
                parallelCommands[index].CacheMode,
                parallelTasks[index].GetAwaiter().GetResult(),
                "bounded-parallel",
                processes,
                samples,
                ref ordinal);
        }

        foreach ((string family, string cacheMode) in IndependentCommandMix())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ordinal++;
            CliObservation observation = RunCommand(
                fixture,
                family,
                cacheMode,
                baselinePath,
                repositoryRoot,
                changePolicyPath,
                cancellationToken);
            RecordIndependentProcess(
                workload,
                family,
                cacheMode,
                observation,
                "sequential",
                processes,
                samples,
                ref ordinal);
        }

        AddInProcessProjections(
            fixture,
            workload,
            processes,
            samples,
            ref ordinal,
            cancellationToken);

        ordinal++;
        CliObservation baseObservation = RunCommand(
            fixture,
            "strict",
            "disabled",
            baselinePath,
            repositoryRoot,
            fixture.PolicyPath,
            cancellationToken);
        BenchmarkCanonicalResultIdentity baseCanonical = SuccessfulCanonicalResult(baseObservation, "base reference");
        BenchmarkProfileSample baseSample = CreateSample(
            workload,
            baseObservation,
            ordinal,
            "disabled",
            baseCanonical,
            isWarmSample: false);
        PreparationProjectionIdentity baseProjection = new()
        {
            CommandFamily = "strict",
            ProjectionId = "synthetic-strict-base",
            ProcessBound = true,
        };
        processes.Add(CreateProcess(baseProjection, ordinal, PreparationRevisionRole.Base,
            PreparationExecutionKind.IndependentProcess, baseSample, baseCanonical, workload));
        samples.Add(baseSample);

        BenchmarkCanonicalResultIdentity candidateCanonical = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate)
            .Select(process => process.CanonicalResult)
            .First();
        BenchmarkEvidenceDocument benchmarkEvidence = BenchmarkEvidenceFactory.Create(
            workload,
            samples[0].RawAnalysisProfile,
            candidateCanonical,
            samples[0]) with
        {
            Samples = samples,
            Run = samples[0].Run,
        };
        PreparedEffectContract effect = CreateEffect(workload, processes);
        CrossProcessPreparationEvidenceDocument evidence = new()
        {
            EvidenceSchemaId = CrossProcessPreparationEvidenceDocument.SchemaId,
            BenchmarkEvidence = benchmarkEvidence,
            Workflow = new CrossProcessPreparationWorkflow
            {
                EvidenceSchemaId = CrossProcessPreparationWorkflow.SchemaId,
                WorkloadIdentity = workload.WorkloadIdentity,
                PreparationBoundary = compilationMode == BenchmarkCompilationMode.RealMsBuild
                    ? PreparationBoundaryKind.MsBuildReceipt
                    : PreparationBoundaryKind.StagedAssemblies,
                CandidateRevision = new PreparationRevisionIdentity
                {
                    Role = PreparationRevisionRole.Candidate,
                    Identity = candidateRevision,
                },
                BaseRevision = new PreparationRevisionIdentity
                {
                    Role = PreparationRevisionRole.Base,
                    Identity = baseRevision,
                },
                Projections = processes.Select(process => process.Identity.Projection).Distinct().ToList(),
                CacheModesMeasured = ["disabled", "miss", "hit"],
                OneProcessAlternativeMeasured = true,
            },
            Processes = processes,
            PreparedEffect = effect,
            Decision = new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.B,
                Route = "defer-prepared-analysis",
                Reason = "The immutable one-process alternative is measured; persisted storage and authorization resources remain unavailable, so cross-process reuse is not authorized by synthetic evidence alone.",
                OneProcessAlternativeEvaluated = true,
                BreakEvenObserved = effect.BreakEvenProcessCount.HasValue,
            },
        };
        evidence.Validate();
        return evidence;
    }

    private static IReadOnlyList<(string Family, string CacheMode)> IndependentCommandMix() =>
    [
        ("no_new_debt", "disabled"),
        ("architecture_health", "disabled"),
        ("change_snapshot", "disabled"),
        ("topology", "disabled"),
        ("measure", "disabled"),
        ("strict", "miss"),
        ("strict", "hit"),
    ];

    private static void RecordIndependentProcess(
        BenchmarkWorkloadDefinition workload,
        string family,
        string cacheMode,
        CliObservation observation,
        string parallelMode,
        ICollection<CrossProcessProcessEvidence> processes,
        ICollection<BenchmarkProfileSample> samples,
        ref int ordinal)
    {
        ordinal++;
        BenchmarkCanonicalResultIdentity canonical = SuccessfulCanonicalResult(observation, family);
        BenchmarkProfileSample sample = CreateSample(
            workload,
            observation,
            ordinal,
            cacheMode,
            canonical,
            isWarmSample: cacheMode == "hit",
            parallelMode: parallelMode);
        PreparationProjectionIdentity projection = new()
        {
            CommandFamily = family,
            ProjectionId = $"synthetic-{family}-candidate-{cacheMode}-{parallelMode}",
            ProcessBound = true,
        };
        processes.Add(CreateProcess(projection, ordinal, PreparationRevisionRole.Candidate,
            PreparationExecutionKind.IndependentProcess, sample, canonical, workload));
        samples.Add(sample);
    }

    private static void AddInProcessProjections(
        BenchmarkMaterializedFixture fixture,
        BenchmarkWorkloadDefinition workload,
        ICollection<CrossProcessProcessEvidence> processes,
        ICollection<BenchmarkProfileSample> samples,
        ref int ordinal,
        CancellationToken cancellationToken)
    {
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        using ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = fixture.PolicyPath,
            PreparationMode = BuildPreparationMode.EnsureBuilt,
            NoRestore = false,
            MaxParallelism = 1,
            CancellationToken = cancellationToken,
        });

        ValidationOutcome strict = snapshot.Evaluate("strict");
        Assert.That(strict.Passed, Is.True,
            $"The in-process strict projection must pass. preflight={strict.PreflightBlocked}; " +
            $"preflight_diagnostics={string.Join(" | ", strict.PreflightDiagnostics.Select(diagnostic => diagnostic.State))}; " +
            $"violations={strict.Violations.Count}; cycles={strict.Cycles.Count}; coverage={strict.CoverageFindings.Count}");
        AddProjection("strict", snapshot.Counters, workload, processes, samples, ref ordinal);

        ValidationOutcome audit = snapshot.Evaluate("audit");
        Assert.That(audit.Passed, Is.True, "The in-process audit projection must pass.");
        AddProjection("audit", snapshot.Counters, workload, processes, samples, ref ordinal);

        snapshot.Measure();
        AddProjection("measure", snapshot.Counters, workload, processes, samples, ref ordinal);
    }

    private static void AddProjection(
        string family,
        ArchitectureAnalysisSnapshotCounters counters,
        BenchmarkWorkloadDefinition workload,
        ICollection<CrossProcessProcessEvidence> processes,
        ICollection<BenchmarkProfileSample> samples,
        ref int ordinal)
    {
        ordinal++;
        BenchmarkCanonicalResultIdentity canonical = BenchmarkIdentity.CreateCanonicalResult("Success", 0, []);
        JsonElement profile = CreateSyntheticProfile(counters, family);
        BenchmarkProfileSample sample = CreateSample(
            workload,
            new CliObservation(0, TimeSpan.Zero, profile),
            ordinal,
            "disabled",
            canonical,
            isWarmSample: true,
            preparedStateMode: "one_process_shared");
        PreparationProjectionIdentity projection = new()
        {
            CommandFamily = family,
            ProjectionId = $"synthetic-{family}-in-process",
            ProcessBound = false,
        };
        processes.Add(CreateProcess(projection, ordinal, PreparationRevisionRole.Candidate,
            PreparationExecutionKind.InProcessProjection, sample, canonical, workload));
        samples.Add(sample);
    }

    private static CrossProcessProcessEvidence CreateProcess(
        PreparationProjectionIdentity projection,
        int ordinal,
        PreparationRevisionRole revisionRole,
        PreparationExecutionKind executionKind,
        BenchmarkProfileSample sample,
        BenchmarkCanonicalResultIdentity canonical,
        BenchmarkWorkloadDefinition workload) => new()
        {
            Identity = new PreparationProcessIdentity
            {
                Projection = projection,
                ProcessOrdinal = ordinal,
                RevisionRole = revisionRole,
                ExecutionKind = executionKind,
            },
            Sample = sample,
            CanonicalResult = canonical,
            Resources = new PreparationResourceEvidence
            {
                StorageBytes = BenchmarkResourceMeasurement.Unavailable("Prepared-state storage is not implemented by issue #493."),
                IoOperations = BenchmarkResourceMeasurement.NotApplicable("No persisted prepared-state I/O exists in the pre-implementation harness."),
                AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Prepared-state allocation is not isolated from the host process."),
                PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Prepared-state memory is not isolated from the host process."),
            },
        };

    private static PreparedEffectContract CreateEffect(
        BenchmarkWorkloadDefinition workload,
        IReadOnlyCollection<CrossProcessProcessEvidence> processes)
    {
        int representativeProcessCount = processes.Count(process =>
            process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
            process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess);
        long candidateWork = workload.Inventory.ProjectCount + workload.Inventory.AssemblyCount +
            workload.Inventory.SourceFileCount + workload.Inventory.TypeCount + workload.Inventory.ReferenceEdgeCount;
        decimal coldPrepareCost = candidateWork;
        decimal loadAuthorizationCost = Math.Max(1, candidateWork / 8m);
        decimal repeatedWorkShare = 0.75m;
        PreparedEffectContract effect = new()
        {
            IssueReference = "#493",
            RepresentativeProcessCount = representativeProcessCount,
            RepeatedWorkShare = repeatedWorkShare,
            CandidatePreparedBoundaryWork = candidateWork,
            CacheAvoidableWork = 0,
            PreparedStateAvoidableWork = candidateWork,
            ColdPrepareCost = coldPrepareCost,
            PerConsumerLoadAuthorizationCost = loadAuthorizationCost,
            BreakEvenProcessCount = null,
            CacheModesMeasured = ["disabled", "miss", "hit"],
            Resources = new PreparationResourceEvidence
            {
                StorageBytes = BenchmarkResourceMeasurement.Unavailable("No persisted prepared-state store exists before implementation."),
                IoOperations = BenchmarkResourceMeasurement.NotApplicable("No persisted prepared-state I/O exists before implementation."),
                AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Prepared-state allocation is not isolated."),
                PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Prepared-state memory is not isolated."),
            },
            ExpectedEffect = CreateExpectedEffect(representativeProcessCount, coldPrepareCost, loadAuthorizationCost, repeatedWorkShare),
            ExactCacheHitSavingsExcluded = true,
        };
        return effect with { BreakEvenProcessCount = effect.CalculateBreakEvenProcessCount() };
    }

    private static BenchmarkExpectedEffectEvidence CreateExpectedEffect(
        int representativeProcessCount,
        decimal coldPrepareCost,
        decimal loadAuthorizationCost,
        decimal repeatedWorkShare)
    {
        decimal Speedup(int count) =>
            (coldPrepareCost * count * repeatedWorkShare) /
            (coldPrepareCost + count * loadAuthorizationCost);
        return new BenchmarkExpectedEffectEvidence
        {
            IssueReference = "#493",
            TargetPhase = "candidate_preparation",
            BaselinePhaseShare = repeatedWorkShare,
            CurrentWorkModel = "R x independent candidate preparation",
            TargetWorkModel = "one cold preparation + R x load/authorization",
            ExpectedLocalSpeedupSmall = Speedup(1),
            ExpectedLocalSpeedupMedium = Speedup(representativeProcessCount),
            ExpectedLocalSpeedupLarge = Speedup(Math.Max(representativeProcessCount * 4, 2)),
            ExpectedEndToEndUpperBound = 1m / ((1m - repeatedWorkShare) + repeatedWorkShare / Math.Max(1m, Speedup(Math.Max(representativeProcessCount, 2)))),
            MemoryAllocationTradeOff = "Storage, I/O, allocation, and peak memory remain explicitly unavailable until a prepared-state design exists.",
            ColdPathTradeOff = "The cold preparation remains a separate cost and is never counted as a cache hit.",
            SuccessThreshold = "Only authorize implementation after one-process sharing is insufficient and the persisted model remains materially cheaper with measured resource bounds.",
            KillCriterion = "Defer or route elsewhere when canonical equivalence, cache separation, or a representative crossover is not reproduced.",
            Confidence = "Synthetic deterministic counter model; timing is environment-labelled observation.",
        };
    }

    private static BenchmarkCanonicalResultIdentity SuccessfulCanonicalResult(CliObservation observation, string command)
    {
        Assert.That(observation.ExitCode, Is.EqualTo(0),
            $"Synthetic issue #493 command '{command}' failed with exit code {observation.ExitCode}.\nstdout: {observation.Output}\nstderr: {observation.Error}");
        return BenchmarkIdentity.CreateCanonicalResult("Success", 0, []);
    }

    private static BenchmarkProfileSample CreateSample(
        BenchmarkWorkloadDefinition workload,
        CliObservation observation,
        int ordinal,
        string cacheMode,
        BenchmarkCanonicalResultIdentity canonical,
        bool isWarmSample,
        string preparedStateMode = "unprepared",
        string parallelMode = "sequential") => new()
        {
            Run = new BenchmarkRunDescriptor
            {
                ExecutionMode = workload.Workflow.ExecutionMode,
                CacheMode = cacheMode,
                PreparedStateMode = preparedStateMode,
                ParallelMode = parallelMode,
                SampleOrdinal = ordinal,
                IsWarmSample = isWarmSample,
            },
            RawAnalysisProfile = observation.Profile,
            CompletionStatus = canonical.CompletionStatus,
            ExitCode = canonical.ExitCode,
            OutputFailed = false,
            WallClock = BenchmarkResourceMeasurement.Available(
            Math.Max(0, (long)observation.Elapsed.TotalMilliseconds), "milliseconds"),
            ProcessorTime = BenchmarkResourceMeasurement.Unavailable("Processor time is not isolated by the pre-implementation harness."),
            AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Allocation is not isolated by the pre-implementation harness."),
            PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Peak memory is not isolated by the pre-implementation harness."),
        };

    private static CliObservation RunCommand(
        BenchmarkMaterializedFixture fixture,
        string family,
        string cacheMode,
        string baselinePath,
        string repositoryRoot,
        string changePolicyPath,
        CancellationToken cancellationToken,
        bool? ensureBuiltOverride = null)
    {
        string policyPath = family == "change_snapshot" ? changePolicyPath : fixture.PolicyPath;
        string profilePath = Path.Combine(fixture.Root, $"profile-{family}-{cacheMode}-{Guid.NewGuid():N}.json");
        string outputPath = Path.Combine(fixture.Root, $"output-{family}-{Guid.NewGuid():N}.json");
        string cachePath = Path.Combine(fixture.Root, "analysis-cache");
        List<string> arguments = family switch
        {
            "strict" => ["--policy", policyPath, "--mode", "strict", "--format", "json", "--profile", profilePath],
            "audit" => ["--policy", policyPath, "--mode", "audit", "--format", "json", "--profile", profilePath],
            "no_new_debt" => ["gate", "--policy", policyPath, "--baseline", baselinePath, "--mode", "all", "--format", "json"],
            "architecture_health" => ["health", "--policy", policyPath, "--baseline", baselinePath, "--mode", "all", "--format", "json", "--execution-context", "synthetic-issue-493"],
            "change_snapshot" => ["change", "snapshot", "--policy", policyPath, "--mode", "strict", "--output", outputPath],
            "topology" => ["topology", "capture", "--policy", policyPath, "--subject-kind", "assembly", "--format", "json", "--output", outputPath],
            "measure" => ["measure", "--policy", policyPath, "--format", "json"],
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown benchmark command family."),
        };

        bool ensureBuilt = ensureBuiltOverride ??
            (fixture.Definition.CompilationMode == BenchmarkCompilationMode.RealMsBuild && family != "change_snapshot");
        if (ensureBuilt)
        {
            arguments.Add("--ensure-built");
        }

        if (cacheMode != "disabled")
        {
            arguments.Add("--cache");
            arguments.Add(cachePath);
        }

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.ArgumentList.Add(CliDllPath(repositoryRoot));
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch clock = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)!;
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new AssertionException($"Synthetic issue #493 command '{family}' exceeded the timeout.");
        }

        clock.Stop();
        string error = standardError.GetAwaiter().GetResult();
        _ = error;
        JsonElement profile = File.Exists(profilePath)
            ? JsonDocument.Parse(File.ReadAllText(profilePath)).RootElement.Clone()
            : CreateSyntheticProfile(null, family);
        return new CliObservation(process.ExitCode, clock.Elapsed, profile, standardOutput.GetAwaiter().GetResult(), error);
    }

    private static JsonElement CreateSyntheticProfile(ArchitectureAnalysisSnapshotCounters? counters, string family)
    {
        return JsonSerializer.SerializeToElement(new
        {
            SchemaId = "analysis-profile/v1",
            CompletionStatus = "Success",
            Projection = family,
            Counters = counters is null
                ? (object)new { Unavailable = "CLI subcommand does not expose profile counters." }
                : counters,
            Phases = Array.Empty<object>(),
            Output = new { OutputFailed = false },
        });
    }

    private static string CliDllPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Release", "net10.0", "ArchLinterNet.Cli.dll");

    private static string CreateChangeSnapshotPolicy(
        BenchmarkMaterializedFixture fixture,
        BenchmarkWorkloadDefinition workload)
    {
        if (workload.CompilationMode == BenchmarkCompilationMode.StagedAssemblies)
        {
            return fixture.PolicyPath;
        }

        string original = File.ReadAllText(fixture.PolicyPath);
        int projectsStart = original.IndexOf("  projects:", StringComparison.Ordinal);
        int contractsStart = original.IndexOf("contracts:", StringComparison.Ordinal);
        if (projectsStart < 0 || contractsStart <= projectsStart)
        {
            throw new InvalidOperationException("Synthetic real-MSBuild policy did not contain its project analysis section.");
        }

        string searchPaths = string.Join(
            Environment.NewLine,
            workload.Projects.Select(project =>
                $"    - src/{project.AssemblyName}/bin/Debug/net10.0"));
        string targetAssemblies = string.Join(
            Environment.NewLine,
            workload.Projects.Select(project => $"    - {project.AssemblyName}"));
        string replacement = $"  target_assemblies:{Environment.NewLine}{targetAssemblies}{Environment.NewLine}  assembly_search_paths:{Environment.NewLine}{searchPaths}{Environment.NewLine}";
        string policy = original[..projectsStart] + replacement + original[contractsStart..];
        string path = Path.Combine(fixture.Root, "change-snapshot-policy.arch.yml");
        File.WriteAllText(path, policy);
        return path;
    }

    private static void WriteRealMsBuildReceipts(BenchmarkMaterializedFixture fixture)
    {
        foreach (BenchmarkProjectNode project in fixture.Definition.Projects)
        {
            string projectPath = Path.Combine(
                fixture.Root,
                "src",
                project.AssemblyName,
                $"{project.AssemblyName}.csproj");
            string assemblyPath = Path.Combine(
                fixture.Root,
                "src",
                project.AssemblyName,
                "bin",
                "Debug",
                "net10.0",
                $"{project.AssemblyName}.dll");
            BuildReceiptStore.Write(
                assemblyPath,
                new BuildReceiptV1(
                    Path.Combine("src", project.AssemblyName, $"{project.AssemblyName}.csproj").Replace('\\', '/'),
                    project.AssemblyName,
                    "Debug",
                    "net10.0",
                    BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, fixture.Root),
                    BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));
        }
    }

    private sealed record CliObservation(
        int ExitCode,
        TimeSpan Elapsed,
        JsonElement Profile,
        string Output = "",
        string Error = "");
}
