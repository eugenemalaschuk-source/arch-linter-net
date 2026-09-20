using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LargeSolutionBenchmarkingTests
{
    [Test]
    public void SameManifest_ProducesSameTopologyInventoryAndIdentity()
    {
        BenchmarkDimensionSet dimensions = new()
        {
            ProjectCount = 8,
            TypesPerProject = 5,
            SourceFilesPerProject = 2,
            ReferencesPerProject = 3,
            LayerCount = 3,
            SelectorMembershipsPerLayer = 4,
            ContractsPerRoot = 2,
            FindingCandidates = 7,
            SourceRootCount = 2,
        };

        BenchmarkWorkloadDefinition first = BenchmarkWorkloadGenerator.Create(
            "synthetic-repeatable-dense", BenchmarkTopologyShape.Dense, dimensions);
        BenchmarkWorkloadDefinition second = BenchmarkWorkloadGenerator.Create(
            "synthetic-repeatable-dense", BenchmarkTopologyShape.Dense, dimensions);

        Assert.That(BenchmarkJson.Serialize(first.ToManifest()), Is.EqualTo(BenchmarkJson.Serialize(second.ToManifest())));
        Assert.That(first.WorkloadIdentity, Is.EqualTo(second.WorkloadIdentity));
        Assert.That(first.Topology, Is.EqualTo(second.Topology));
        Assert.That(first.Inventory, Is.EqualTo(second.Inventory));
    }

    [Test]
    public void RequiredGraphShapes_ReportDeclaredTopology()
    {
        var cases = new[]
        {
            (BenchmarkTopologyShape.Linear, 5, false, 4),
            (BenchmarkTopologyShape.WideFanOutFanIn, 5, false, 6),
            (BenchmarkTopologyShape.Diamond, 5, false, 5),
            (BenchmarkTopologyShape.Dense, 5, false, 10),
            (BenchmarkTopologyShape.CyclicScc, 5, true, 5),
        };

        foreach ((BenchmarkTopologyShape shape, int projectCount, bool cycle, int edgeCount) in cases)
        {
            BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
                $"synthetic-{shape.ToString().ToLowerInvariant()}",
                shape,
                new BenchmarkDimensionSet { ProjectCount = projectCount });

            Assert.Multiple(() =>
            {
                Assert.That(workload.Topology.ContainsCycle, Is.EqualTo(cycle), shape.ToString());
                Assert.That(workload.Topology.ReferenceEdgeCount, Is.EqualTo(edgeCount), shape.ToString());
                Assert.That(workload.Edges.Distinct().Count(), Is.EqualTo(workload.Edges.Count), shape.ToString());
            });
        }
    }

    [Test]
    public void ChangingOneDimension_PreservesUnrelatedTopology()
    {
        BenchmarkWorkloadDefinition smallTypes = BenchmarkWorkloadGenerator.Create(
            "synthetic-dimension-small", BenchmarkTopologyShape.WideFanOutFanIn,
            new BenchmarkDimensionSet { ProjectCount = 6, TypesPerProject = 2 });
        BenchmarkWorkloadDefinition manyTypes = BenchmarkWorkloadGenerator.Create(
            "synthetic-dimension-large", BenchmarkTopologyShape.WideFanOutFanIn,
            new BenchmarkDimensionSet { ProjectCount = 6, TypesPerProject = 11 });

        Assert.Multiple(() =>
        {
            Assert.That(smallTypes.WorkloadIdentity, Is.Not.EqualTo(manyTypes.WorkloadIdentity));
            Assert.That(smallTypes.Projects, Is.EqualTo(manyTypes.Projects));
            Assert.That(smallTypes.Edges, Is.EqualTo(manyTypes.Edges));
            Assert.That(smallTypes.Topology, Is.EqualTo(manyTypes.Topology));
            Assert.That(smallTypes.Inventory.TypeCount, Is.Not.EqualTo(manyTypes.Inventory.TypeCount));
            Assert.That(smallTypes.Inventory.ProjectCount, Is.EqualTo(manyTypes.Inventory.ProjectCount));
        });
    }

    [Test]
    public void ProjectAndTypeScaleProfilesAreAvailableAsNamedWorkloads()
    {
        BenchmarkWorkloadDefinition manyProjects = BenchmarkWorkloadGenerator.Create(
            "synthetic-many-projects-few-types",
            BenchmarkTopologyShape.ManyProjectsFewTypes);
        BenchmarkWorkloadDefinition manyTypes = BenchmarkWorkloadGenerator.Create(
            "synthetic-few-projects-many-types",
            BenchmarkTopologyShape.FewProjectsManyTypes);

        Assert.Multiple(() =>
        {
            Assert.That(manyProjects.Inventory.ProjectCount, Is.GreaterThan(manyTypes.Inventory.ProjectCount));
            Assert.That(manyProjects.Inventory.TypeCount, Is.LessThan(manyTypes.Inventory.TypeCount));
            Assert.That(manyProjects.Shape, Is.EqualTo(BenchmarkTopologyShape.ManyProjectsFewTypes));
            Assert.That(manyTypes.Shape, Is.EqualTo(BenchmarkTopologyShape.FewProjectsManyTypes));
        });
    }

    [Test]
    public void ReferenceDimensionAddsDeterministicEdgesWithoutChangingProjectCount()
    {
        BenchmarkWorkloadDefinition sparse = BenchmarkWorkloadGenerator.Create(
            "synthetic-edge-sparse", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 8, ReferencesPerProject = 2 });
        BenchmarkWorkloadDefinition dense = BenchmarkWorkloadGenerator.Create(
            "synthetic-edge-dense", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 8, ReferencesPerProject = 5 });

        Assert.Multiple(() =>
        {
            Assert.That(dense.Inventory.ReferenceEdgeCount, Is.GreaterThan(sparse.Inventory.ReferenceEdgeCount));
            Assert.That(dense.Inventory.ProjectCount, Is.EqualTo(sparse.Inventory.ProjectCount));
        });
    }

    [Test]
    public void WorkloadIdMustBeSynthetic()
    {
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create("private-adopter", BenchmarkTopologyShape.Linear),
            Throws.ArgumentException);
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create("synthetic-private/name", BenchmarkTopologyShape.Linear),
            Throws.ArgumentException);
    }

    [Test]
    public void Materializer_WritesBoundedRealAndStagedFixtureShapes()
    {
        BenchmarkWorkloadDefinition real = BenchmarkWorkloadGenerator.Create(
            "synthetic-materialized-real", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 3, TypesPerProject = 3, SourceFilesPerProject = 2 });
        BenchmarkWorkloadDefinition staged = real with
        {
            WorkloadId = "synthetic-materialized-staged",
            CompilationMode = BenchmarkCompilationMode.StagedAssemblies,
        };

        using BenchmarkMaterializedFixture realFixture = BenchmarkFixtureMaterializer.Materialize(real);
        using BenchmarkMaterializedFixture stagedFixture = BenchmarkFixtureMaterializer.Materialize(staged);

        Assert.Multiple(() =>
        {
            Assert.That(realFixture.ProjectPaths, Has.Count.EqualTo(3));
            Assert.That(realFixture.SourcePaths, Has.Count.EqualTo(6));
            Assert.That(File.Exists(realFixture.SolutionPath), Is.True);
            Assert.That(File.Exists(realFixture.PolicyPath), Is.True);
            Assert.That(File.Exists(Path.Combine(stagedFixture.Root, ".benchmark", "staged-assemblies", "manifest.json")), Is.True);
        });
    }

    [Test]
    public void CanonicalResultIdentity_NormalizesTemporaryRootsButPreservesOrder()
    {
        BenchmarkFindingIdentity firstFinding = new()
        {
            ContractId = "synthetic-contract-001",
            Kind = "dependency",
            SourceAssembly = "Synthetic.Project001",
            SourceType = "Synthetic.Project001.Type001",
            SourceMember = "Identity",
            Location = "/private/tmp/run-a/src/Synthetic.Project001/SourceRoot01/Type001.cs",
        };
        BenchmarkFindingIdentity equivalent = firstFinding with
        {
            Location = "C:\\temp\\run-b\\src\\Synthetic.Project001\\SourceRoot01\\Type001.cs",
        };
        BenchmarkCanonicalResultIdentity first = BenchmarkIdentity.CreateCanonicalResult("Success", 0, [firstFinding]);
        BenchmarkCanonicalResultIdentity second = BenchmarkIdentity.CreateCanonicalResult("Success", 0, [equivalent]);
        BenchmarkCanonicalResultIdentity changed = BenchmarkIdentity.CreateCanonicalResult(
            "Success", 0, [equivalent with { SourceMember = "Different" }]);

        Assert.Multiple(() =>
        {
            Assert.That(first.Sha256, Is.EqualTo(second.Sha256));
            Assert.That(first.Sha256, Is.Not.EqualTo(changed.Sha256));
        });
    }

    [Test]
    public void WorkloadManifest_ValidatesAgainstVersionedSchema()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-manifest-contract", BenchmarkTopologyShape.Dense,
            new BenchmarkDimensionSet { ProjectCount = 5, SourceRootCount = 2 });
        string json = BenchmarkJson.Serialize(workload.ToManifest());
        JsonSchema schema = JsonSchema.FromText(LoadWorkloadSchema());
        EvaluationResults validation = schema.Evaluate(
            JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.That(validation.IsValid, Is.True, Describe(validation));
    }

    [Test]
    public void Evidence_RoundTripsAndValidatesAgainstVersionedSchema()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-evidence-contract", BenchmarkTopologyShape.Diamond,
            new BenchmarkDimensionSet { ProjectCount = 4, FindingCandidates = 3 },
            BenchmarkCompilationMode.StagedAssemblies,
            BenchmarkExecutionMode.MultiCommand,
            independentProcesses: 3);
        using JsonDocument profile = JsonDocument.Parse("""
            {
              "SchemaId": "analysis-profile/v1",
              "CompletionStatus": "Success",
              "Counters": {},
              "Phases": [],
              "Output": { "OutputFailed": false }
            }
            """);
        BenchmarkCanonicalResultIdentity result = BenchmarkIdentity.CreateCanonicalResult("Success", 0, []);
        BenchmarkEvidenceDocument evidence = BenchmarkEvidenceFactory.Create(workload, profile.RootElement.Clone(), result);
        string json = BenchmarkEvidenceJson.Serialize(evidence);
        BenchmarkEvidenceDocument roundTrip = BenchmarkEvidenceJson.Deserialize(json);
        JsonSchema schema = JsonSchema.FromText(LoadEvidenceSchema());
        EvaluationResults validation = schema.Evaluate(
            JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.Multiple(() =>
        {
            Assert.That(roundTrip.Workload.WorkloadIdentity, Is.EqualTo(workload.WorkloadIdentity));
            Assert.That(roundTrip.Samples.Single().RawAnalysisProfile.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
            Assert.That(validation.IsValid, Is.True, Describe(validation));
        });
    }

    [Test]
    public void Evidence_RejectsMissingProfileAndFailedOutputWithSuccessExit()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-evidence-failure", BenchmarkTopologyShape.Linear);
        using JsonDocument invalidProfile = JsonDocument.Parse("{\"SchemaId\":\"wrong/v1\"}");
        BenchmarkCanonicalResultIdentity result = BenchmarkIdentity.CreateCanonicalResult("Success", 0, []);

        Assert.That(
            () => BenchmarkEvidenceFactory.Create(workload, invalidProfile.RootElement.Clone(), result),
            Throws.InvalidOperationException);
    }

    [Test]
    public void Evidence_RetainsMultiSizeComplexityAndExpectedEffectContract()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-effect-contract", BenchmarkTopologyShape.Dense);
        using JsonDocument profile = JsonDocument.Parse("{\"SchemaId\":\"analysis-profile/v1\"}");
        BenchmarkComplexityEvidence complexity = new()
        {
            ScaleVariable = "P=projects",
            CurrentWorkModel = "O(P x E)",
            ObservedGrowth = "deterministic edge traversals grow with the project matrix",
            TargetWorkModel = "O(P + E)",
            Disposition = BenchmarkEvidenceDisposition.Candidate,
            ScalePoints =
            [
                new() { Label = "small", ScaleValue = 4, DeterministicWork = 12, FindingCount = 0, AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Not measured.") },
                new() { Label = "medium", ScaleValue = 8, DeterministicWork = 48, FindingCount = 0, AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Not measured.") },
                new() { Label = "large", ScaleValue = 16, DeterministicWork = 192, FindingCount = 0, AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Not measured.") },
            ],
        };
        BenchmarkExpectedEffectEvidence expectedEffect = new()
        {
            IssueReference = "#655",
            TargetPhase = "graph_traversal",
            BaselinePhaseShare = 0.4m,
            CurrentWorkModel = "O(P x E)",
            TargetWorkModel = "O(P + E)",
            ExpectedLocalSpeedupSmall = 1.1m,
            ExpectedLocalSpeedupMedium = 1.5m,
            ExpectedLocalSpeedupLarge = 2m,
            ExpectedEndToEndUpperBound = 1.4m,
            MemoryAllocationTradeOff = "Index memory must remain bounded by the edge set.",
            ColdPathTradeOff = "Index construction is measured separately from warm reuse.",
            SuccessThreshold = "Deterministic traversal work falls by at least one repeated factor at medium and large sizes.",
            KillCriterion = "Defer if the work reduction is not reproduced at three sizes or canonical results differ.",
            Confidence = "medium; timing remains environment-dependent",
        };
        BenchmarkEvidenceDocument evidence = BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            BenchmarkIdentity.CreateCanonicalResult("Success", 0, []),
            complexity: complexity,
            expectedEffect: expectedEffect);

        string first = BenchmarkEvidenceJson.Serialize(evidence);
        string second = BenchmarkEvidenceJson.Serialize(BenchmarkEvidenceJson.Deserialize(first));

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second, Does.Contain("success_threshold"));
            Assert.That(second, Does.Contain("scale_points"));
        });
    }

    [Test]
    public void EvidenceDoesNotContainPrivatePathsOrRawLogs()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-privacy-check", BenchmarkTopologyShape.Linear);
        using JsonDocument profile = JsonDocument.Parse("{\"SchemaId\":\"analysis-profile/v1\"}");
        string json = BenchmarkEvidenceJson.Serialize(BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            BenchmarkIdentity.CreateCanonicalResult("Success", 0, [])));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("/Users/"));
            Assert.That(json, Does.Not.Contain("private-adopter"));
            Assert.That(json, Does.Not.Contain("raw CI log"));
        });
    }

    [Test]
    public void ExplicitRealMsBuildMaterializer_BuildsRepresentativeFixture()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-buildable-linear", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 2, TypesPerProject = 2, SourceFilesPerProject = 1, SourceRootCount = 2 });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);

        fixture.Build();

        Assert.That(Directory.GetFiles(fixture.Root, "*.dll", SearchOption.AllDirectories), Is.Not.Empty);
    }

    [Test]
    public void ExplicitRealMsBuildMaterializer_IsAcceptedByExistingCliBoundary()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-cli-linear", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 2, TypesPerProject = 1, SourceFilesPerProject = 1 });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);

        fixture.Build();
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string cliPath = Path.Combine(
            repositoryRoot,
            "src",
            "ArchLinterNet.Cli",
            "bin",
            "Debug",
            "net10.0",
            "ArchLinterNet.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(cliPath);
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(fixture.PolicyPath);
        startInfo.ArgumentList.Add("--strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--ensure-built");

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Multiple(() =>
        {
            Assert.That(process.ExitCode, Is.EqualTo(0), $"stdout: {output}{Environment.NewLine}stderr: {error}");
            Assert.That(() => JsonDocument.Parse(output), Throws.Nothing);
        });
    }

    private static string LoadEvidenceSchema()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return File.ReadAllText(Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "benchmark-evidence-v1.schema.json"));
    }

    private static string LoadWorkloadSchema()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return File.ReadAllText(Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "large-solution-workload-v1.schema.json"));
    }

    private static string Describe(EvaluationResults results) =>
        string.Join(Environment.NewLine, results.Details.Where(detail => !detail.IsValid).Select(detail =>
            $"{detail.EvaluationPath}: {string.Join(",", detail.Errors?.Values ?? [])}"));
}
