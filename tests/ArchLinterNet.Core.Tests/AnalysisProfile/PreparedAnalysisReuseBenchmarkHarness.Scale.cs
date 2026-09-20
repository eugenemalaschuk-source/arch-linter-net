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

            points.Add(new MeasuredScalePoint(
                label,
                workload,
                measurements.Count,
                measurements.Sum(measurement => measurement.PreparationWork),
                measurements.Sum(measurement => measurement.ProjectionWork)));
        }

        return points;
    }
}
