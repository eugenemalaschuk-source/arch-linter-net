using System.Diagnostics;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditExecutionTests
{
    private static readonly string[] _expectedMetricIds =
    [
        "synthetic-metric-001",
        "synthetic-metric-002",
        "synthetic-metric-003",
    ];

    [TestCase(nameof(BenchmarkContractLayerGraphShape.DenseDag), false)]
    [TestCase(nameof(BenchmarkContractLayerGraphShape.LayeredDag), false)]
    [TestCase(nameof(BenchmarkContractLayerGraphShape.StronglyConnectedWithAcyclicTail), true)]
    public void BuildableContractGraph_ExecutesCycleSiblingAndMethodBodyPathsWithEquivalentOutputs(
        string graphShapeName,
        bool expectCycleOutput)
    {
        BenchmarkContractLayerGraphShape graphShape = Enum.Parse<BenchmarkContractLayerGraphShape>(graphShapeName);
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            $"synthetic-audit-execution-{graphShape.ToString().ToLowerInvariant()}",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ProjectCount = 2,
                TypesPerProject = 3,
                SourceFilesPerProject = 1,
                ReferencesPerProject = 1,
                LayerCount = 4,
                ContractsPerWorkload = 1,
                FindingCandidates = 1,
                IgnoreRuleCount = 1,
                MetricRuleCount = 3,
                ReportRuleCount = 2,
                MethodBodyRuleCount = 1,
            },
            contractLayerGraphShape: graphShape);
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        Stopwatch build = Stopwatch.StartNew();
        fixture.Build();
        build.Stop();
        BenchmarkResourceMeasurement buildMeasurement = BenchmarkResourceMeasurement.Available(
            Math.Max(1, (long)Math.Ceiling(build.Elapsed.TotalMilliseconds)), "milliseconds");
        BenchmarkResourceMeasurement materializationMeasurement = BenchmarkResourceMeasurement.Unavailable(
            "Fixture materialization duration is not needed for this deterministic checker-invocation test.");

        BenchmarkAuditAnalysisRun unobserved = BenchmarkAuditAnalysisRunner.Run(
            fixture, observerEnabled: false, sampleOrdinal: 1, isWarmSample: false,
            parallelMode: "sequential", maxParallelism: 1,
            materializationMeasurement, buildMeasurement);
        BenchmarkAuditAnalysisRun observed = BenchmarkAuditAnalysisRunner.Run(
            fixture, observerEnabled: true, sampleOrdinal: 1, isWarmSample: false,
            parallelMode: "sequential", maxParallelism: 1,
            materializationMeasurement, buildMeasurement);

        Assert.Multiple(() =>
        {
            Assert.That(observed.TimedContractFamilyCounts["cycle"], Is.GreaterThan(0));
            Assert.That(observed.TimedContractFamilyCounts["acyclic_sibling"], Is.GreaterThan(0));
            Assert.That(observed.TimedContractFamilyCounts["method_body"], Is.GreaterThan(0));
            Assert.That(observed.TimedContractFamilyCounts["dependency"], Is.EqualTo(1));
            Assert.That(observed.Outcome.Cycles.Count > 0, Is.EqualTo(expectCycleOutput));
            Assert.That(observed.Sample.MetricWork!.MetricCount, Is.EqualTo(3));
            Assert.That(observed.Sample.MetricWork.IsComplete, Is.True);
            Assert.That(observed.Sample.MetricWork.Results.Select(result => result.MetricId),
                Is.EqualTo(_expectedMetricIds));
            Assert.That(observed.Sample.MetricWork.Results.All(result =>
                result.MetricKind == "topology_type_count" && result.Value > 0 &&
                result.Value == result.ContributorCount), Is.True);
            Assert.That(observed.Sample.MetricWork.ResultsSha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(observed.Sample.RawAnalysisProfile.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
            Assert.That(observed.Sample.LoadedAssemblies!.Artifacts, Is.Not.Empty);
            Assert.That(observed.Sample.LoadedAssemblies.Artifacts.All(artifact =>
                artifact.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && artifact.Sha256.Length == 64), Is.True);
            Assert.That(observed.Sample.LoadedAssemblies.Sha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(observed.Sample.WorkCounters!.ProjectFileParses.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.SourceFactFileParses.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.MethodBodyCompilationCalls.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.MethodBodySyntaxTrees.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.AstSemanticLookups.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.LayerMembershipProbes.Value, Is.GreaterThan(0));
            Assert.That(observed.Sample.WorkCounters!.CelContextConstructions.Value, Is.GreaterThan(0));
            Assert.That(observed.CanonicalResult.Sha256, Is.EqualTo(unobserved.CanonicalResult.Sha256));
            Assert.That(observed.MaterializedInputSha256, Is.EqualTo(unobserved.MaterializedInputSha256));
            Assert.That(observed.Sample.LoadedAssemblies!.Sha256,
                Is.EqualTo(unobserved.Sample.LoadedAssemblies!.Sha256));
        });
    }
}
