using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditWorkloadTests
{
    [Test]
    public void DefaultDimensions_OmitNewZeroFieldsAndKeepStableIdentity()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-stable-default", BenchmarkTopologyShape.Linear);
        string manifest = BenchmarkJson.Serialize(workload.ToManifest());

        Assert.Multiple(() =>
        {
            Assert.That(manifest, Does.Not.Contain("ignore_rule_count"));
            Assert.That(manifest, Does.Not.Contain("metric_rule_count"));
            Assert.That(manifest, Does.Not.Contain("report_rule_count"));
            Assert.That(manifest, Does.Not.Contain("contract_layer_graph"));
            Assert.That(workload.Inventory.IgnoreRuleCount, Is.Zero);
            Assert.That(workload.Inventory.MetricRuleCount, Is.Zero);
            Assert.That(workload.Inventory.ReportRuleCount, Is.Zero);
            Assert.That(workload.ContractLayerGraph, Is.Null);
        });
        Assert.That(workload.WorkloadIdentity, Is.EqualTo("bbc7e7a001c76e0363dd0f068fed74eac34726a3b5242c4df002e9ec0b24badf"));
    }

    [Test]
    public void AuditDimensions_MaterializeIndependentCountsAndExecutableCycleWithTail()
    {
        BenchmarkDimensionSet dimensions = new()
        {
            ProjectCount = 4,
            TypesPerProject = 2,
            SourceFilesPerProject = 1,
            ReferencesPerProject = 1,
            LayerCount = 5,
            IgnoreRuleCount = 2,
            MetricRuleCount = 3,
            ReportRuleCount = 4,
        };
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-audit-contract-graph",
            BenchmarkTopologyShape.Linear,
            dimensions,
            contractLayerGraphShape: BenchmarkContractLayerGraphShape.StronglyConnectedWithAcyclicTail);

        Assert.Multiple(() =>
        {
            Assert.That(workload.Topology.ContainsCycle, Is.False, "The project graph must remain buildable.");
            Assert.That(workload.Inventory.IgnoreRuleCount, Is.EqualTo(2));
            Assert.That(workload.Inventory.MetricRuleCount, Is.EqualTo(3));
            Assert.That(workload.Inventory.ReportRuleCount, Is.EqualTo(4));
            Assert.That(workload.Inventory.MethodBodyRuleCount, Is.Zero);
            Assert.That(workload.Inventory.ContractLayerNodeCount, Is.EqualTo(5));
            Assert.That(workload.Inventory.ContractLayerReferenceCount, Is.EqualTo(5));
            Assert.That(workload.ContractLayerGraph!.Edges, Is.EqualTo(new[]
            {
                new BenchmarkContractLayerEdge("contract_layer_01", "contract_layer_02"),
                new BenchmarkContractLayerEdge("contract_layer_02", "contract_layer_01"),
                new BenchmarkContractLayerEdge("contract_layer_02", "contract_layer_03"),
                new BenchmarkContractLayerEdge("contract_layer_03", "contract_layer_04"),
                new BenchmarkContractLayerEdge("contract_layer_04", "contract_layer_05"),
            }));
        });

        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        string policy = File.ReadAllText(fixture.PolicyPath);
        string source = File.ReadAllText(Path.Combine(fixture.Root, "src", "Synthetic.Project001", "SourceRoot01", "ContractLayerGraph.cs"));
        string reportRules = File.ReadAllText(Path.Combine(fixture.Root, ".benchmark", "report-rules.json"));
        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain("strict_cycles:"));
            Assert.That(policy, Does.Contain("strict_acyclic_siblings:"));
            Assert.That(policy, Does.Contain("contract_layer_05"));
            Assert.That(policy, Does.Contain("ignored_violations:"));
            Assert.That(policy, Does.Contain("synthetic-metric-003"));
            Assert.That(source, Does.Contain("Layer02.ContractLayer02? ContractLayer02Reference"));
            Assert.That(source, Does.Contain("Layer01.ContractLayer01? ContractLayer01Reference"));
            Assert.That(reportRules, Does.Contain("synthetic-report-rule-004"));
        });

        fixture.Build();
    }

    [Test]
    public void MethodBodyWorkload_GeneratesIndependentRuleAndSyntaxInputDimensions()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-method-body-audit", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { MethodBodyRuleCount = 2, ReportRuleCount = 3 });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        string policy = File.ReadAllText(fixture.PolicyPath);
        string source = File.ReadAllText(Path.Combine(fixture.Root, "src", "Synthetic.Project001", "SourceRoot01", "Type001.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(workload.Inventory.MethodBodyRuleCount, Is.EqualTo(2));
            Assert.That(policy, Does.Contain("strict_method_body:"));
            Assert.That(policy, Does.Contain("synthetic-method-body-002"));
            Assert.That(source, Does.Contain("System.Console.WriteLine(Identity)"));
        });
    }

    [Test]
    public void IgnoreDimension_SuppressesItsGeneratedForbiddenTypeReferences()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-ignore-execution-audit",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ProjectCount = 2,
                TypesPerProject = 4,
                SourceFilesPerProject = 1,
                ReferencesPerProject = 0,
                LayerCount = 2,
                ContractsPerWorkload = 1,
                IgnoreRuleCount = 3,
            });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        fixture.Build();
        BenchmarkAuditAnalysisRun run = BenchmarkAuditAnalysisRunner.Run(
            fixture,
            observerEnabled: false,
            sampleOrdinal: 1,
            isWarmSample: false,
            parallelMode: "sequential",
            maxParallelism: 1,
            BenchmarkResourceMeasurement.Unavailable("This is a deterministic checker-path test."),
            BenchmarkResourceMeasurement.Available(1, "milliseconds"));

        Assert.Multiple(() =>
        {
            Assert.That(run.Outcome.Violations, Is.Empty);
            Assert.That(run.Outcome.UnmatchedIgnoredViolations, Is.Empty);
            Assert.That(run.Outcome.Passed, Is.True);
        });
    }

    [TestCase(nameof(BenchmarkContractLayerGraphShape.DenseDag), 4, 6)]
    [TestCase(nameof(BenchmarkContractLayerGraphShape.LayeredDag), 4, 3)]
    public void AcyclicContractGraphs_HaveDeclaredForwardEdges(
        string shapeName,
        int layerCount,
        int expectedEdges)
    {
        BenchmarkContractLayerGraphShape shape = Enum.Parse<BenchmarkContractLayerGraphShape>(shapeName);
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-contract-dag",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { LayerCount = layerCount },
            contractLayerGraphShape: shape);

        Assert.Multiple(() =>
        {
            Assert.That(workload.Topology.ContainsCycle, Is.False);
            Assert.That(workload.ContractLayerGraph!.Edges, Has.Count.EqualTo(expectedEdges));
            Assert.That(workload.ContractLayerGraph.Edges.All(edge =>
                int.Parse(edge.FromLayer[^2..]) < int.Parse(edge.ToLayer[^2..])), Is.True);
        });
    }

    [Test]
    public void ProjectCyclicScc_RemainsStructuralOnlyAndFailsClosed()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-project-cycle", BenchmarkTopologyShape.CyclicScc);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BenchmarkFixtureMaterializer.Materialize(workload))!;

        Assert.That(exception.Message, Does.Contain("structural-only"));
    }
}
