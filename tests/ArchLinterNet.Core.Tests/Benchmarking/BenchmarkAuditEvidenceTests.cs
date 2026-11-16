using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditEvidenceTests
{
    private static readonly int[] _expectedMetricValues = [6, 7];

    [Test]
    public void LegacyEvidence_OmitsOptionalAuditFields()
    {
        BenchmarkEvidenceDocument evidence = CreateEvidence();

        string json = BenchmarkEvidenceJson.Serialize(evidence);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("work_counters"));
            Assert.That(json, Does.Not.Contain("timing_boundaries"));
            Assert.That(json, Does.Not.Contain("source_revision"));
            Assert.That(json, Does.Not.Contain("materialized_input_sha256"));
        });
    }

    [Test]
    public void AuditedEvidence_RoundTripsFixedCountersTimerBoundariesAndProvenance()
    {
        BenchmarkWorkObserver observer = new();
        observer.RecordProjectFileParse();
        observer.RecordSourceFactFileParse();
        observer.RecordSourceFactFileParse();
        observer.RecordMethodBodyCompilation(3);
        observer.RecordAstSemanticLookup();
        observer.RecordLayerMembershipProbe();
        observer.RecordCelContextConstruction();

        BenchmarkWorkloadDefinition metricWorkload = BenchmarkWorkloadGenerator.Create(
            "synthetic-audited-metric-evidence",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet { MetricRuleCount = 2 });
        BenchmarkEvidenceDocument metricTemplate = CreateEvidence(metricWorkload);
        BenchmarkProfileSample sample = metricTemplate.Samples.Single() with
        {
            WorkCounters = BenchmarkWorkCounterEvidence.FromObserver(observer),
            MetricWork = CreateMetricWorkEvidence(2),
            LoadedAssemblies = BenchmarkLoadedAssemblyEvidence.FromLoadedArtifacts(
            [
                new ArchitectureLoadedAssemblyArtifact(
                    Path.Combine(Path.GetTempPath(), "synthetic", "bin", "Synthetic.Project000.dll"),
                    new string('c', 64),
                    string.Empty,
                    2048),
            ]),
            HistoryWorkCounters = BenchmarkHistoryWorkCounterEvidence.NotApplicableToCoreWorkload(),
            TimingBoundaries = CreateTimingBoundaries(),
            ReportOutputSha256 = new string('b', 64),
        };
        BenchmarkEvidenceDocument evidence = metricTemplate with
        {
            Samples = [sample],
            Environment = CreateEvidence().Environment with
            {
                SourceRevision = "a293f8746f0c9d3b77f14fb852e7d4f97629e843",
                RunnerIdentity = "local-linux-x64-dotnet-10",
                MaterializedInputSha256 = new string('a', 64),
            },
        };

        string json = BenchmarkEvidenceJson.Serialize(evidence);
        using JsonDocument serializedEvidence = JsonDocument.Parse(json);
        JsonElement serializedSample = serializedEvidence.RootElement.GetProperty("samples").EnumerateArray().Single();
        bool samplesAreMultiline = serializedSample.GetRawText().Contains('\n');
        string archiveJson = BenchmarkEvidenceJson.SerializeArchive(json);
        using JsonDocument archive = JsonDocument.Parse(archiveJson);
        string archiveSha256 = archive.RootElement.GetProperty("payload_sha256").GetString()!;
        bool archiveChunksAreBounded = archive.RootElement.GetProperty("payload_base64_chunks")
            .EnumerateArray()
            .All(chunk => chunk.GetString() is { Length: > 0 and <= 4096 });
        BenchmarkEvidenceDocument roundTrip = BenchmarkEvidenceJson.Deserialize(archiveJson);
        BenchmarkEvidenceDocument directRoundTrip = BenchmarkEvidenceJson.Deserialize(json);
        string repeatedArchive = BenchmarkEvidenceJson.SerializeArchive(json);
        JsonSchema schema = JsonSchemaTextFactory.FromText(LoadEvidenceSchema());
        EvaluationResults validation = schema.Evaluate(
            JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });
        JsonSchema archiveSchema = JsonSchemaTextFactory.FromText(LoadArchiveEvidenceSchema());
        EvaluationResults archiveValidation = archiveSchema.Evaluate(
            JsonNode.Parse(archiveJson), new EvaluationOptions { OutputFormat = OutputFormat.List });

        evidence.Validate();
        Assert.Multiple(() =>
        {
            Assert.That(roundTrip.Samples.Single().WorkCounters!.ProjectFileParses.Value, Is.EqualTo(1));
            Assert.That(roundTrip.Samples.Single().WorkCounters!.SourceFactFileParses.Value, Is.EqualTo(2));
            Assert.That(roundTrip.Samples.Single().WorkCounters!.MethodBodySyntaxTrees.Value, Is.EqualTo(3));
            Assert.That(roundTrip.Samples.Single().WorkCounters!.CelContextConstructions.Value, Is.EqualTo(1));
            Assert.That(roundTrip.Samples.Single().HistoryWorkCounters!.AncestryAncestorsVisited.Status, Is.EqualTo(BenchmarkMeasurementStatus.NotApplicable));
            Assert.That(roundTrip.Samples.Single().TimingBoundaries!.ProcessHostStartupAndShutdown.Status, Is.EqualTo(BenchmarkMeasurementStatus.Unavailable));
            Assert.That(roundTrip.Environment.MaterializedInputSha256, Is.EqualTo(new string('a', 64)));
            Assert.That(roundTrip.Samples.Single().ReportOutputSha256, Is.EqualTo(new string('b', 64)));
            Assert.That(roundTrip.Samples.Single().MetricWork!.MetricCount, Is.EqualTo(2));
            Assert.That(roundTrip.Samples.Single().MetricWork!.Results.Select(result => result.Value), Is.EqualTo(_expectedMetricValues));
            Assert.That(roundTrip.Samples.Single().LoadedAssemblies!.Artifacts.Single().FileName,
                Is.EqualTo("Synthetic.Project000.dll"));
            Assert.That(roundTrip.Samples.Single().LoadedAssemblies!.Artifacts.Single().Sha256, Is.EqualTo(new string('c', 64)));
            Assert.That(roundTrip.Samples.Single().LoadedAssemblies!.Sha256,
                Is.EqualTo(sample.LoadedAssemblies.Sha256));
            Assert.That(roundTrip.Samples.Single().RawAnalysisProfile.GetProperty("Output")
                .GetProperty("OutputFailed").GetBoolean(), Is.False);
            Assert.That(directRoundTrip.Samples.Single().RawAnalysisProfile.GetProperty("Phases").GetArrayLength(),
                Is.EqualTo(0));
            Assert.That(samplesAreMultiline, Is.True,
                "Large measurement samples must remain indented in committed evidence JSON.");
            Assert.That(archiveChunksAreBounded, Is.True);
            Assert.That(repeatedArchive, Is.EqualTo(archiveJson), "Evidence archives must be deterministic.");
            Assert.That(validation.IsValid, Is.True, Describe(validation));
            Assert.That(archiveValidation.IsValid, Is.True, Describe(archiveValidation));
        });

        string corruptedArchiveDigest = archiveJson.Replace(archiveSha256, new string('0', 64), StringComparison.Ordinal);
        Assert.That(() => BenchmarkEvidenceJson.Deserialize(corruptedArchiveDigest),
            Throws.InvalidOperationException.With.Message.Contains("SHA-256"));
    }

    [Test]
    public void AuditedEvidence_RejectsNegativeWorkCount()
    {
        BenchmarkWorkCounterEvidence invalid = BenchmarkWorkCounterEvidence.FromObserver(new BenchmarkWorkObserver()) with
        {
            CelContextConstructions = BenchmarkResourceMeasurement.Available(-1, "operations"),
        };

        Assert.That(() => invalid.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void AuditedEvidence_ExtendedWorkloadDimensionsAndContractGraph_ValidateAgainstSchema()
    {
        BenchmarkDimensionSet dimensions = new()
        {
            ProjectCount = 2,
            TypesPerProject = 3,
            SourceFilesPerProject = 2,
            ReferencesPerProject = 0,
            LayerCount = 4,
            ContractsPerWorkload = 1,
            IgnoreRuleCount = 2,
            MetricRuleCount = 3,
            ReportRuleCount = 4,
            MethodBodyRuleCount = 5,
        };
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-audit-schema-extended",
            BenchmarkTopologyShape.Linear,
            dimensions,
            compilationMode: BenchmarkCompilationMode.RealMsBuild,
            contractLayerGraphShape: BenchmarkContractLayerGraphShape.DenseDag);
        string json = BenchmarkEvidenceJson.Serialize(CreateEvidence(workload));
        JsonSchema schema = JsonSchemaTextFactory.FromText(LoadEvidenceSchema());
        EvaluationResults validation = schema.Evaluate(
            JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.That(validation.IsValid, Is.True, Describe(validation));
    }

    private static BenchmarkEvidenceDocument CreateEvidence(BenchmarkWorkloadDefinition? workload = null)
    {
        workload ??= BenchmarkWorkloadGenerator.Create("synthetic-audit-evidence", BenchmarkTopologyShape.Linear);
        using JsonDocument profile = JsonDocument.Parse("""
            {
              "SchemaId": "analysis-profile/v1",
              "CompletionStatus": "Success",
              "Counters": {},
              "Phases": [],
              "Output": { "OutputFailed": false }
            }
            """);
        BenchmarkProfileSample? sample = workload.Dimensions.MetricRuleCount == 0
            ? null
            : new BenchmarkProfileSample
            {
                Run = new BenchmarkRunDescriptor
                {
                    ExecutionMode = workload.Workflow.ExecutionMode,
                    CacheMode = "disabled",
                    PreparedStateMode = "unprepared",
                    ParallelMode = "sequential",
                    SampleOrdinal = 1,
                    IsWarmSample = false,
                },
                RawAnalysisProfile = profile.RootElement.Clone(),
                CompletionStatus = "Success",
                ExitCode = 0,
                OutputFailed = false,
                WallClock = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no wall-clock measurement."),
                ProcessorTime = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no processor-time measurement."),
                AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no allocation measurement."),
                PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no memory measurement."),
                MetricWork = CreateMetricWorkEvidence(workload.Dimensions.MetricRuleCount),
            };
        return BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            BenchmarkIdentity.CreateCanonicalResult("Success", 0, []),
            sample);
    }

    private static BenchmarkMetricWorkEvidence CreateMetricWorkEvidence(int metricCount)
    {
        BenchmarkMetricResultEvidence[] results = Enumerable.Range(1, metricCount)
            .Select(index => new BenchmarkMetricResultEvidence
            {
                MetricId = $"synthetic-metric-{index:000}",
                MetricKind = "topology_type_count",
                Value = index + 5,
                ContributorCount = index + 5,
            })
            .ToArray();
        return new BenchmarkMetricWorkEvidence
        {
            MetricCount = results.Length,
            IsComplete = true,
            Results = results,
            ResultsSha256 = BenchmarkIdentity.Sha256(BenchmarkJson.Serialize(results)),
        };
    }

    private static BenchmarkTimingBoundaryEvidence CreateTimingBoundaries() => new()
    {
        Contract = BenchmarkTimingBoundaryEvidence.BoundaryContract,
        FixtureMaterialization = BenchmarkResourceMeasurement.Available(20, "milliseconds"),
        RestoreAndBuild = BenchmarkResourceMeasurement.NotApplicable("Staged assemblies were already prepared."),
        ProcessHostStartupAndShutdown = BenchmarkResourceMeasurement.Unavailable(
            "No process handshake isolates host startup and shutdown from analyzer execution."),
        CliProcess = BenchmarkResourceMeasurement.Available(110, "milliseconds"),
        CoreAnalysisSession = BenchmarkResourceMeasurement.Available(90, "milliseconds"),
        ResultPublication = BenchmarkResourceMeasurement.Available(4, "milliseconds"),
        TotalEndToEnd = BenchmarkResourceMeasurement.Available(125, "milliseconds"),
        ProfilerOverhead = new BenchmarkProfilerOverheadEvidence
        {
            Instrumentation = BenchmarkProfilerOverheadEvidence.InstrumentationId,
            ObserverDisabledAnalysis = BenchmarkResourceMeasurement.Available(90, "milliseconds"),
            ObserverEnabledAnalysis = BenchmarkResourceMeasurement.Available(91, "milliseconds"),
            ObserverDisabledAllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Allocation sample not collected."),
            ObserverEnabledAllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Allocation sample not collected."),
            ObserverDisabledPeakRetainedBytes = BenchmarkResourceMeasurement.Unavailable("Peak retained memory was not collected."),
            ObserverEnabledPeakRetainedBytes = BenchmarkResourceMeasurement.Unavailable("Peak retained memory was not collected."),
            CanonicalResultsMatch = true,
        },
    };

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

    private static string LoadArchiveEvidenceSchema()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return File.ReadAllText(Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "benchmark-evidence-archive-v1.schema.json"));
    }

    private static string Describe(EvaluationResults results) =>
        string.Join(Environment.NewLine, results.Details?.Where(detail => !detail.IsValid).Select(detail =>
            $"{detail.EvaluationPath}: {string.Join(",", detail.Errors?.Values.ToArray() ?? [])}").ToArray() ?? []);
}
