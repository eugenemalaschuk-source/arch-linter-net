using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditEvidenceTests
{
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

        BenchmarkProfileSample sample = CreateEvidence().Samples.Single() with
        {
            WorkCounters = BenchmarkWorkCounterEvidence.FromObserver(observer),
            HistoryWorkCounters = BenchmarkHistoryWorkCounterEvidence.NotApplicableToCoreWorkload(),
            TimingBoundaries = CreateTimingBoundaries(),
            ReportOutputSha256 = new string('b', 64),
        };
        BenchmarkEvidenceDocument evidence = CreateEvidence() with
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
        BenchmarkEvidenceDocument roundTrip = BenchmarkEvidenceJson.Deserialize(json);
        JsonSchema schema = JsonSchemaTextFactory.FromText(LoadEvidenceSchema());
        EvaluationResults validation = schema.Evaluate(
            JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

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
            Assert.That(validation.IsValid, Is.True, Describe(validation));
        });
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

    private static BenchmarkEvidenceDocument CreateEvidence()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-audit-evidence", BenchmarkTopologyShape.Linear);
        using JsonDocument profile = JsonDocument.Parse("""
            {
              "SchemaId": "analysis-profile/v1",
              "CompletionStatus": "Success",
              "Counters": {},
              "Phases": [],
              "Output": { "OutputFailed": false }
            }
            """);
        return BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            BenchmarkIdentity.CreateCanonicalResult("Success", 0, []));
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

    private static string Describe(EvaluationResults results) =>
        string.Join(Environment.NewLine, results.Details?.Where(detail => !detail.IsValid).Select(detail =>
            $"{detail.EvaluationPath}: {string.Join(",", detail.Errors?.Values.ToArray() ?? [])}").ToArray() ?? []);
}
