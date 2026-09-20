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
                CounterWorkMeasurement? measurement = TryReadCounterWork(observation.Profile);
                Assert.That(measurement, Is.Not.Null,
                    $"Scale point '{label}' command family '{family}' must expose real profile counters.");
                measurements.Add(measurement!);
            }

            decimal perConsumerLoadAuthorizationCost = MeasureScaleLoadAuthorizationCost(
                fixture,
                workload,
                cancellationToken);

            points.Add(new MeasuredScalePoint(
                label,
                workload,
                measurements.Count,
                measurements.Sum(measurement => measurement.PreparationWork),
                measurements.Sum(measurement => measurement.ProjectionWork),
                perConsumerLoadAuthorizationCost));
        }

        return points;
    }

    private static decimal MeasureScaleLoadAuthorizationCost(
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

        ValidationOutcome strict = snapshot.Evaluate("strict");
        Assert.That(strict.Passed, Is.True,
            "The scale-specific strict projection must pass before measuring load/authorization work.");
        CounterWorkMeasurement strictMeasurement = ReadRequiredCounterWork(snapshot.Counters, "scale/strict");

        ValidationOutcome audit = snapshot.Evaluate("audit");
        Assert.That(audit.Passed, Is.True,
            "The scale-specific audit projection must pass before measuring load/authorization work.");
        CounterWorkMeasurement auditMeasurement = ReadRequiredCounterWork(snapshot.Counters, "scale/audit");

        decimal loadAuthorizationCost =
            (strictMeasurement.LoadAuthorizationWork + auditMeasurement.LoadAuthorizationWork) / 2m;
        Assert.That(loadAuthorizationCost, Is.GreaterThan(0),
            "Scale-specific load/authorization cost must come from positive measured selected-state counters.");
        return loadAuthorizationCost;
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
