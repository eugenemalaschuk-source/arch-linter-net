using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
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
            SelectorPredicateTermsPerLayer = 4,
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
                new BenchmarkDimensionSet { ProjectCount = projectCount, ReferencesPerProject = 1 });

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
    public void ReferenceDimensionChangesLinearGraphAtOneToTwo()
    {
        BenchmarkWorkloadDefinition one = BenchmarkWorkloadGenerator.Create(
            "synthetic-linear-one-reference", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 8, ReferencesPerProject = 1 });
        BenchmarkWorkloadDefinition two = BenchmarkWorkloadGenerator.Create(
            "synthetic-linear-two-references", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 8, ReferencesPerProject = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(two.Inventory.ReferenceEdgeCount, Is.GreaterThan(one.Inventory.ReferenceEdgeCount));
            Assert.That(two.Edges, Is.Not.EqualTo(one.Edges));
            Assert.That(two.Topology.ContainsCycle, Is.False);
        });
    }

    [Test]
    public void ReferenceDimensionPreservesAcyclicTopologyForAcyclicShapes()
    {
        BenchmarkWorkloadDefinition linear = BenchmarkWorkloadGenerator.Create(
            "synthetic-linear-reference-independence",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 8, ReferencesPerProject = 5 });
        BenchmarkWorkloadDefinition veryLarge = BenchmarkWorkloadGenerator.CreateVeryLargeSynthetic();

        Assert.Multiple(() =>
        {
            Assert.That(linear.Topology.ContainsCycle, Is.False);
            Assert.That(linear.Topology.StronglyConnectedComponentCount, Is.EqualTo(linear.Projects.Count));
            Assert.That(veryLarge.Topology.ContainsCycle, Is.False);
            Assert.That(veryLarge.Topology.StronglyConnectedComponentCount, Is.EqualTo(veryLarge.Projects.Count));
            Assert.That(veryLarge.Inventory.SelectorPredicateEvaluationCount, Is.EqualTo(65_536));
            Assert.That(veryLarge.Inventory.FindingCandidateCount, Is.EqualTo(128));
        });
    }

    [Test]
    public void CyclicSccFailsClosedAsStructuralOnly()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-cyclic-structural-only", BenchmarkTopologyShape.CyclicScc,
            new BenchmarkDimensionSet { ProjectCount = 3 });

        Assert.That(
            () => BenchmarkFixtureMaterializer.Materialize(workload),
            Throws.InvalidOperationException.With.Message.Contains("structural-only"));
    }

    [Test]
    public void SourceRootDimensionDoesNotInventTypes()
    {
        BenchmarkDimensionSet dimensions = new()
        {
            ProjectCount = 2,
            TypesPerProject = 5,
            SourceFilesPerProject = 2,
            SourceRootCount = 4,
        };
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-source-root-type-count", BenchmarkTopologyShape.Linear, dimensions);
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);

        int materializedTypeCount = fixture.SourcePaths
            .Sum(path => File.ReadAllLines(path).Count(line => line.StartsWith("public sealed class ", StringComparison.Ordinal)));

        Assert.Multiple(() =>
        {
            Assert.That(workload.Inventory.TypeCount, Is.EqualTo(dimensions.ProjectCount * dimensions.TypesPerProject));
            Assert.That(materializedTypeCount, Is.EqualTo(workload.Inventory.TypeCount));
        });
    }

    [Test]
    public void SelectorAndFindingDimensionsMaterializeAnalyzerWork()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-selector-finding-work",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ProjectCount = 2,
                TypesPerProject = 2,
                SourceFilesPerProject = 1,
                LayerCount = 2,
                SelectorPredicateTermsPerLayer = 3,
                FindingCandidates = 2,
            });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        string policy = File.ReadAllText(fixture.PolicyPath);

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain("classification:"));
            Assert.That(
                policy.Split("subject.simpleName.startsWith", StringSplitOptions.None).Length - 1,
                Is.EqualTo(workload.Dimensions.LayerCount * workload.Dimensions.SelectorPredicateTermsPerLayer));
            Assert.That(
                workload.Inventory.SelectorPredicateEvaluationCount,
                Is.EqualTo(workload.Inventory.TypeCount * workload.Dimensions.LayerCount * workload.Dimensions.SelectorPredicateTermsPerLayer));
            Assert.That(policy, Does.Contain("synthetic-finding-candidate-001"));
            Assert.That(policy, Does.Contain("subject.namespace == 'Synthetic.Synthetic.Project001'"));
            Assert.That(policy, Does.Contain("subject.simpleName == 'SourceRoot01Type001'"));
            Assert.That(policy, Does.Contain("forbidden_name_prefix: SourceRoot"));
        });

        fixture.Build();
        BenchmarkCliRun run = fixture.RunValidation();
        Assert.Multiple(() =>
        {
            Assert.That(run.ExitCode, Is.Not.EqualTo(0));
            Assert.That(run.StandardOutput, Does.Contain("synthetic-finding-candidate-001"));
            using JsonDocument output = JsonDocument.Parse(run.StandardOutput);
            Assert.That(output.RootElement.GetProperty("violations").GetArrayLength(), Is.EqualTo(workload.Inventory.FindingCandidateCount));
        });
    }

    [Test]
    public void FindingCandidatesCannotExceedMaterializedTypes()
    {
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create(
                "synthetic-too-many-finding-candidates",
                BenchmarkTopologyShape.Linear,
                new BenchmarkDimensionSet
                {
                    ProjectCount = 1,
                    TypesPerProject = 1,
                    FindingCandidates = 2,
                }),
            Throws.ArgumentException);
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
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create("synthetic-Foo", BenchmarkTopologyShape.Linear),
            Throws.ArgumentException);
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create("synthetic-é", BenchmarkTopologyShape.Linear),
            Throws.ArgumentException);
        Assert.That(
            () => BenchmarkWorkloadGenerator.Create("synthetic-", BenchmarkTopologyShape.Linear),
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
            Assert.That(stagedFixture.StagedAssemblyPaths, Has.Count.EqualTo(3));
            Assert.That(stagedFixture.StagedAssemblyPaths.All(path => File.Exists(BuildReceiptStore.ReceiptPathFor(path))), Is.True);
        });
    }

    [Test]
    public void StagedAssembliesAreAcceptedAsExternalInputsWithoutEnsureBuilt()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-staged-cli", BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { ProjectCount = 2, TypesPerProject = 1, SourceFilesPerProject = 1 },
            BenchmarkCompilationMode.StagedAssemblies);
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);

        BenchmarkCliRun run = fixture.RunValidation(ensureBuilt: false);

        Assert.Multiple(() =>
        {
            Assert.That(run.ExitCode, Is.EqualTo(0), $"stdout: {run.StandardOutput}{Environment.NewLine}stderr: {run.StandardError}");
            Assert.That(() => JsonDocument.Parse(run.StandardOutput), Throws.Nothing);
            Assert.That(fixture.StagedAssemblyPaths, Has.Count.EqualTo(workload.Inventory.AssemblyCount));
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
    public void EvidenceMeasurementsFailClosedInTypedAndSchemaValidation()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-evidence-measurement-invariants", BenchmarkTopologyShape.Linear);
        using JsonDocument profile = JsonDocument.Parse("{\"SchemaId\":\"analysis-profile/v1\"}");
        BenchmarkEvidenceDocument evidence = BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            BenchmarkIdentity.CreateCanonicalResult("Success", 0, []));
        BenchmarkProfileSample invalidSample = evidence.Samples.Single() with
        {
            WallClock = new BenchmarkResourceMeasurement { Status = BenchmarkMeasurementStatus.Available },
        };

        Assert.That(
            () => (evidence with { Samples = [invalidSample] }).Validate(),
            Throws.InvalidOperationException);

        JsonObject invalidJson = JsonNode.Parse(BenchmarkEvidenceJson.Serialize(evidence))!.AsObject();
        invalidJson["samples"]![0]!["wall_clock"] = new JsonObject
        {
            ["status"] = "unavailable",
            ["value"] = 0,
            ["unit"] = null,
            ["reason"] = null,
        };
        JsonSchema schema = JsonSchema.FromText(LoadEvidenceSchema());
        EvaluationResults validation = schema.Evaluate(
            invalidJson, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.That(validation.IsValid, Is.False, Describe(validation));
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
        BenchmarkCliRun run = fixture.RunValidation();

        Assert.Multiple(() =>
        {
            Assert.That(run.ExitCode, Is.EqualTo(0), $"stdout: {run.StandardOutput}{Environment.NewLine}stderr: {run.StandardError}");
            Assert.That(() => JsonDocument.Parse(run.StandardOutput), Throws.Nothing);
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
