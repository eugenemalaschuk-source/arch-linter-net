using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class PreparedAnalysisReuseBenchmarkHarness
{
    private static IReadOnlyList<MeasuredScalePoint> MeasureScaleEvidence(
        BenchmarkCompilationMode compilationMode,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        string modeId = compilationMode == BenchmarkCompilationMode.RealMsBuild
            ? "real-msbuild"
            : "staged-assemblies";
        (string Label, BenchmarkDimensionSet Dimensions)[] cases =
        [
            ("small", new BenchmarkDimensionSet
            {
                ProjectCount = 1,
                TypesPerProject = 8,
                SourceFilesPerProject = 2,
                ReferencesPerProject = 0,
                LayerCount = 2,
                SelectorPredicateTermsPerLayer = 4,
                ContractsPerWorkload = 2,
                SourceRootCount = 1,
            }),
            ("medium", new BenchmarkDimensionSet
            {
                ProjectCount = 1,
                TypesPerProject = 32,
                SourceFilesPerProject = 8,
                ReferencesPerProject = 0,
                LayerCount = 4,
                SelectorPredicateTermsPerLayer = 8,
                ContractsPerWorkload = 4,
                SourceRootCount = 2,
            }),
            ("large", new BenchmarkDimensionSet
            {
                ProjectCount = 4,
                TypesPerProject = 32,
                SourceFilesPerProject = 8,
                ReferencesPerProject = 1,
                LayerCount = 4,
                SelectorPredicateTermsPerLayer = 8,
                ContractsPerWorkload = 4,
                SourceRootCount = 2,
            }),
        ];

        List<MeasuredScalePoint> points = [];
        foreach ((string label, BenchmarkDimensionSet dimensions) in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-cross-process-scale-{modeId}-{label}",
                BenchmarkTopologyShape.ManyProjectsFewTypes,
                dimensions,
                compilationMode,
                BenchmarkExecutionMode.FullGovernance,
                independentProcesses: _measuredCommandFamilies.Length);
            using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
            if (compilationMode == BenchmarkCompilationMode.RealMsBuild)
            {
                fixture.Build();
                WriteRealMsBuildReceipts(fixture);
            }

            string baselinePath = Path.Combine(fixture.Root, "empty-baseline.arch.yml");
            File.WriteAllText(baselinePath, "version: 3\nbaseline: {}\nmetric_baselines: []\n");
            string changePolicyPath = CreateChangeSnapshotPolicy(fixture, workload);
            List<CounterWorkMeasurement> measurements = [];
            List<CliObservation> observations = [];
            foreach (string family in _measuredCommandFamilies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CliObservation observation = RunCommand(
                    fixture,
                    family,
                    "disabled",
                    baselinePath,
                    repositoryRoot,
                    changePolicyPath,
                    cancellationToken);
                _ = SuccessfulCanonicalResult(observation, $"{label}/{family}");
                observations.Add(observation);
                CounterWorkMeasurement? measurement = TryReadCounterWork(observation.Profile);
                Assert.That(measurement, Is.Not.Null,
                    $"Scale point '{label}' command family '{family}' must expose real profile counters.");
                measurements.Add(measurement!);
            }

            ScaleTimingMeasurement scaleTiming = MeasureScaleTiming(
                fixture,
                workload,
                cancellationToken);
            IReadOnlyDictionary<string, decimal> projectionMillisecondsByFamily =
                _measuredCommandFamilies
                    .Select((family, index) =>
                    {
                        decimal projectionDuration = scaleTiming.ProjectionMillisecondsByFamily.TryGetValue(
                            family,
                            out decimal measuredProjection)
                            ? measuredProjection
                            : DurationMilliseconds(observations[index].Elapsed);
                        return (family, projectionDuration);
                    })
                    .ToDictionary(item => item.family, item => item.projectionDuration, StringComparer.Ordinal);
            decimal independentPreparationMilliseconds = observations
                .Select((observation, index) =>
                {
                    decimal independentDuration = DurationMilliseconds(observation.Elapsed);
                    decimal projectionDuration = projectionMillisecondsByFamily[_measuredCommandFamilies[index]];
                    Assert.That(independentDuration, Is.GreaterThanOrEqualTo(projectionDuration),
                        $"Scale point '{label}' has a projection duration greater than its independent process duration for '{_measuredCommandFamilies[index]}'.");
                    return independentDuration - projectionDuration;
                })
                .Sum();

            points.Add(new MeasuredScalePoint(
                label,
                workload,
                measurements.Count,
                measurements.Sum(measurement => measurement.PreparationWork),
                measurements.Sum(measurement => measurement.ProjectionWork),
                independentPreparationMilliseconds,
                projectionMillisecondsByFamily.Values.Sum(),
                measurements.Sum(measurement => measurement.LoadAuthorizationWork) / measurements.Count,
                scaleTiming.LoadAuthorizationMilliseconds));
        }

        return points;
    }

    private sealed record ScaleTimingMeasurement(
        decimal LoadAuthorizationMilliseconds,
        IReadOnlyDictionary<string, decimal> ProjectionMillisecondsByFamily);

    private static ScaleTimingMeasurement MeasureScaleTiming(
        BenchmarkMaterializedFixture fixture,
        BenchmarkWorkloadDefinition workload,
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

        Stopwatch strictClock = Stopwatch.StartNew();
        ValidationOutcome strict = snapshot.Evaluate("strict");
        strictClock.Stop();
        Assert.That(strict.Passed, Is.True,
            "The scale-specific strict projection must pass before measuring load/authorization work.");
        _ = ReadRequiredCounterWork(snapshot.Counters, "scale/strict");

        Stopwatch auditClock = Stopwatch.StartNew();
        ValidationOutcome audit = snapshot.Evaluate("audit");
        auditClock.Stop();
        Assert.That(audit.Passed, Is.True,
            "The scale-specific audit projection must pass before measuring load/authorization work.");
        _ = ReadRequiredCounterWork(snapshot.Counters, "scale/audit");

        JsonElement strictProfile = CreateSyntheticProfile(snapshot.Counters, "scale-strict");
        JsonElement auditProfile = CreateSyntheticProfile(snapshot.Counters, "scale-audit");
        decimal loadAuthorizationMilliseconds =
            (MeasureLoadAuthorizationDuration(strictProfile) + MeasureLoadAuthorizationDuration(auditProfile)) / 2m;
        IReadOnlyDictionary<string, decimal> projectionMillisecondsByFamily = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["strict"] = DurationMilliseconds(strictClock.Elapsed),
            ["audit"] = DurationMilliseconds(auditClock.Elapsed),
        };
        Assert.That(loadAuthorizationMilliseconds, Is.GreaterThan(0),
            "Scale-specific load/authorization duration must be positive.");
        Assert.That(projectionMillisecondsByFamily.Values.Sum(), Is.GreaterThan(0),
            "Scale-specific in-process projection duration must be positive.");
        return new ScaleTimingMeasurement(loadAuthorizationMilliseconds, projectionMillisecondsByFamily);
    }

    private static CounterWorkMeasurement ReadRequiredCounterWork(
        ArchitectureAnalysisSnapshotCounters counters,
        string projection)
    {
        CounterWorkMeasurement? measurement = TryReadCounterWork(CreateSyntheticProfile(counters, projection));
        Assert.That(measurement, Is.Not.Null,
            $"Scale-specific projection '{projection}' must expose real profile and selected-state counters.");
        return measurement!;
    }
}
