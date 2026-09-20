using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Reporting;
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
public sealed partial class PreparedAnalysisReuseBenchmarkHarness
{
    private const int ProcessTimeoutMilliseconds = 300_000;
    private const string EvidencePath = "docs/internal/prepared-analysis-reuse-evidence.json";
    private static readonly string[] _measuredCommandFamilies =
    [
        "strict",
        "audit",
        "no_new_debt",
        "architecture_health",
        "change_snapshot",
        "topology",
        "measure",
    ];

    private static readonly string[] _sharedProjectionFamilies = ["strict", "audit"];

    private static readonly string[] _canonicalResultFields =
    [
        "passed",
        "mode",
        "violations",
        "cycles",
        "cycle_diagnostics",
        "coverage_findings",
        "unmatched_ignored_violations",
        "policy_consistency_findings",
        "coverage_summary",
        "classification_conflicts",
        "classification_metadata_failures",
        "classification_roles",
        "classification_path_deferred",
        "source_set_expansion",
        "subtractive_matcher_participation",
    ];

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

        AddProcessBoundOneProcessProjections(
            workload,
            processes,
            samples,
            ref ordinal);

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
            ComparisonGroup = "strict-base",
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
        IReadOnlyList<MeasuredScalePoint> measuredScalePoints = MeasureScaleEvidence(
            compilationMode,
            repositoryRoot,
            cancellationToken);
        PreparedEffectContract effect = CreateEffect(workload, processes, measuredScalePoints);
        CrossProcessPreparationWorkflow workflow = new()
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
            MeasuredCommandFamilies = _measuredCommandFamilies,
            CacheModesMeasured = ["disabled", "miss", "hit"],
            OneProcessAlternativeMeasured = effect.OneProcessWorkEvidenceComplete,
        };
        CrossProcessPreparationEvidenceDocument evidence = new()
        {
            EvidenceSchemaId = CrossProcessPreparationEvidenceDocument.SchemaId,
            BenchmarkEvidence = benchmarkEvidence,
            Workflow = workflow,
            Processes = processes,
            PreparedEffect = effect,
            Decision = CreateDecision(effect, workflow),
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
            ComparisonGroup = ComparisonGroupFor(family),
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
            PreparationMode = workload.CompilationMode == BenchmarkCompilationMode.RealMsBuild
                ? BuildPreparationMode.EnsureBuilt
                : BuildPreparationMode.Ordinary,
            NoRestore = false,
            MaxParallelism = 1,
            CancellationToken = cancellationToken,
        });

        ValidationOutcome strict = snapshot.Evaluate("strict");
        Assert.That(strict.Passed, Is.True,
            $"The in-process strict projection must pass. preflight={strict.PreflightBlocked}; " +
            $"preflight_diagnostics={string.Join(" | ", strict.PreflightDiagnostics.Select(diagnostic => diagnostic.State))}; " +
            $"violations={strict.Violations.Count}; cycles={strict.Cycles.Count}; coverage={strict.CoverageFindings.Count}");
        AddProjection("strict", snapshot.Counters, strict, workload, processes, samples, ref ordinal);

        ValidationOutcome audit = snapshot.Evaluate("audit");
        Assert.That(audit.Passed, Is.True, "The in-process audit projection must pass.");
        AddProjection("audit", snapshot.Counters, audit, workload, processes, samples, ref ordinal);
    }

    private static void AddProcessBoundOneProcessProjections(
        BenchmarkWorkloadDefinition workload,
        ICollection<CrossProcessProcessEvidence> processes,
        ICollection<BenchmarkProfileSample> samples,
        ref int ordinal)
    {
        IReadOnlyList<CrossProcessProcessEvidence> processBoundSources = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess &&
                              !_sharedProjectionFamilies.Contains(process.Identity.Projection.CommandFamily, StringComparer.Ordinal))
            .GroupBy(process => process.Identity.Projection.CommandFamily, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        foreach (CrossProcessProcessEvidence source in processBoundSources)
        {
            ordinal++;
            BenchmarkProfileSample sample = source.Sample with
            {
                Run = source.Sample.Run with
                {
                    SampleOrdinal = ordinal,
                    PreparedStateMode = "one_process_process_bound",
                },
            };
            PreparationProjectionIdentity projection = source.Identity.Projection with
            {
                ProjectionId = $"{source.Identity.Projection.ProjectionId}-one-process-process-bound",
            };
            processes.Add(CreateProcess(
                projection,
                ordinal,
                PreparationRevisionRole.Candidate,
                PreparationExecutionKind.ProcessBoundProjection,
                sample,
                source.CanonicalResult,
                workload));
            samples.Add(sample);
        }
    }

    private static void AddProjection(
        string family,
        ArchitectureAnalysisSnapshotCounters counters,
        ValidationOutcome outcome,
        BenchmarkWorkloadDefinition workload,
        ICollection<CrossProcessProcessEvidence> processes,
        ICollection<BenchmarkProfileSample> samples,
        ref int ordinal)
    {
        ordinal++;
        string output = FormatProjectionResult(family, outcome);
        BenchmarkCanonicalResultIdentity canonical = SuccessfulCanonicalResult(
            new CliObservation(0, TimeSpan.Zero, CreateSyntheticProfile(counters, family), output),
            family);
        JsonElement profile = CreateSyntheticProfile(counters, family);
        BenchmarkProfileSample sample = CreateSample(
            workload,
            new CliObservation(0, TimeSpan.Zero, profile, output),
            ordinal,
            "disabled",
            canonical,
            isWarmSample: true,
            preparedStateMode: "one_process_shared");
        PreparationProjectionIdentity projection = new()
        {
            CommandFamily = family,
            ProjectionId = $"synthetic-{family}-in-process",
            ComparisonGroup = ComparisonGroupFor(family),
            ProcessBound = false,
        };
        processes.Add(CreateProcess(projection, ordinal, PreparationRevisionRole.Candidate,
            PreparationExecutionKind.InProcessProjection, sample, canonical, workload));
        samples.Add(sample);
    }

    private static string FormatProjectionResult(string family, ValidationOutcome outcome) =>
        ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            family,
            outcome.Passed,
            outcome.Violations,
            outcome.Cycles,
            outcome.CycleFindings,
            outcome.ClassificationRoles,
            outcome.ClassificationPathDeferred,
            outcome.PreflightDiagnostics,
            outcome.SourceExpansion,
            outcome.CoverageFindings,
            outcome.UnmatchedIgnoredViolations,
            outcome.PolicyConsistencyFindings,
            outcome.CoverageSummaries,
            outcome.ClassificationConflicts,
            outcome.ClassificationMetadataFailures,
            outcome.SubtractiveMatcherParticipation);

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

    private static BenchmarkExpectedEffectEvidence CreateExpectedEffect(
        decimal repeatedWorkShare,
        IReadOnlyList<PreparedEffectScalePoint> scaleEvidence)
    {
        PreparedEffectScalePoint small = scaleEvidence.Single(point => point.Label == "small");
        PreparedEffectScalePoint medium = scaleEvidence.Single(point => point.Label == "medium");
        PreparedEffectScalePoint large = scaleEvidence.Single(point => point.Label == "large");
        return new BenchmarkExpectedEffectEvidence
        {
            IssueReference = "#493",
            TargetPhase = "candidate_preparation",
            BaselinePhaseShare = repeatedWorkShare,
            CurrentWorkModel = "R x independent candidate preparation/fact work",
            TargetWorkModel = "one cold preparation + R x load/authorization + unavoidable projection/command work",
            ExpectedLocalSpeedupSmall = small.ExpectedLocalSpeedup,
            ExpectedLocalSpeedupMedium = medium.ExpectedLocalSpeedup,
            ExpectedLocalSpeedupLarge = large.ExpectedLocalSpeedup,
            ExpectedEndToEndUpperBound = 1m / ((1m - repeatedWorkShare) + repeatedWorkShare / Math.Max(1m, medium.ExpectedLocalSpeedup)),
            MemoryAllocationTradeOff = "Direct storage, I/O, allocation, and peak-memory measurements remain unavailable; bounded pre-implementation sensitivity ranges are recorded and must be replaced by store instrumentation.",
            ColdPathTradeOff = "The cold preparation remains a separate cost and is never counted as a cache hit.",
            SuccessThreshold = "Only authorize implementation when persisted reuse is at least 10% cheaper than the measured representative one-process alternative, the measured small/medium/large matrix is decision-capable, and resource bounds are available.",
            KillCriterion = "Defer or route elsewhere when canonical equivalence, cache separation, or a representative crossover is not reproduced.",
            Confidence = "Expected effect and small/medium/large scaling are derived from measured analysis-profile counters; persisted load/authorization remains an explicit proxy bounded by observed one-process projection work.",
        };
    }

    private static BenchmarkCanonicalResultIdentity SuccessfulCanonicalResult(CliObservation observation, string command)
    {
        Assert.That(observation.ExitCode, Is.EqualTo(0),
            $"Synthetic issue #493 command '{command}' failed with exit code {observation.ExitCode}.\nstdout: {observation.Output}\nstderr: {observation.Error}");
        string completionStatus = observation.Profile.TryGetProperty("CompletionStatus", out JsonElement status) &&
            status.ValueKind == JsonValueKind.String
            ? status.GetString()!
            : "Success";
        string canonicalText = ExtractCanonicalResult(observation.Output);
        return BenchmarkIdentity.CreateCanonicalResultFromCanonicalText(
            completionStatus,
            observation.ExitCode,
            canonicalText,
            CountCanonicalFindings(observation.Output));
    }

    private static string ComparisonGroupFor(string family) =>
        family switch
        {
            "strict" => "strict-validation",
            "audit" => "audit-validation",
            _ => $"{family}-process-bound",
        };

    private static string ExtractCanonicalResult(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !_canonicalResultFields.Any(field => root.TryGetProperty(field, out _)))
            {
                return BenchmarkIdentity.NormalizeJson(output);
            }

            return string.Join(
                "\n",
                _canonicalResultFields.Select(field =>
                    root.TryGetProperty(field, out JsonElement value) ? value.GetRawText() : "null"));
        }
        catch (JsonException)
        {
            return output.Trim();
        }
    }

    private static int CountCanonicalFindings(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return 0;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            return _canonicalResultFields
                .Where(field => field is "violations" or "cycles" or "cycle_diagnostics" or "coverage_findings" or
                    "unmatched_ignored_violations" or "policy_consistency_findings" or "coverage_summary" or
                    "classification_conflicts" or "classification_metadata_failures" or "classification_roles" or
                    "preflight_diagnostics" or "subtractive_matcher_participation")
                .Where(field => root.TryGetProperty(field, out _))
                .Where(field => root.GetProperty(field).ValueKind == JsonValueKind.Array)
                .Sum(field => root.GetProperty(field).GetArrayLength());
        }
        catch (JsonException)
        {
            return 0;
        }
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
            "no_new_debt" => ["gate", "--policy", policyPath, "--baseline", baselinePath, "--mode", "all", "--format", "json", "--profile", profilePath],
            "architecture_health" => ["health", "--policy", policyPath, "--baseline", baselinePath, "--mode", "all", "--format", "json", "--execution-context", "synthetic-issue-493", "--profile", profilePath],
            "change_snapshot" => ["change", "snapshot", "--policy", policyPath, "--mode", "strict", "--output", outputPath, "--profile", profilePath],
            "topology" => ["topology", "capture", "--policy", policyPath, "--subject-kind", "assembly", "--format", "json", "--output", outputPath, "--profile", profilePath],
            "measure" => ["measure", "--policy", policyPath, "--format", "json", "--profile", profilePath],
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
        string standardOutputText = standardOutput.GetAwaiter().GetResult();
        string output = File.Exists(outputPath) ? File.ReadAllText(outputPath) : standardOutputText;
        return new CliObservation(process.ExitCode, clock.Elapsed, profile, output, error);
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
