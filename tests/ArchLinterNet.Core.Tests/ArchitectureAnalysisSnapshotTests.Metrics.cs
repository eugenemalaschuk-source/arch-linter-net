using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using static ArchLinterNet.Core.Tests.ArchitectureAnalysisSnapshotTests;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSnapshotMetricsTests
{
    [Test]
    public void Measure_MissingResolvedTargetAssembly_IsUnassessableForEverySelectedMetric()
    {
        Fixture fixture = CreateFixture();
        ArchitectureContractDocument document = fixture.RunnerSetupService.DocumentToReturn;
        document.Metrics =
        [
            new ArchitectureMetricDefinition
            {
                Id = "missing-target",
                Kind = ArchitectureMetricKinds.TopologyTypeCount,
                TopologyNode = "component",
            },
            new ArchitectureMetricDefinition
            {
                Id = "otherwise-independent-public-surface",
                Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                PublicApiSurface = "surface",
            },
        ];
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<Assembly>(),
            ["Missing.Target"],
            Array.Empty<string>());
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(new ArchitectureAnalysisSession(
            context,
            document,
            selectedContractIds: null,
            enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null));

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        ArchitectureMetricMeasurementOutcome outcome = snapshot.Measure(["otherwise-independent-public-surface"]);
        ArchitectureMetricMeasurement measurement = outcome.Measurements.Single();
        ArchitectureApplicabilityRecord record = outcome.Applicability!.Controls.Single().Record!;

        Assert.Multiple(() =>
        {
            Assert.That(measurement.Id, Is.EqualTo("otherwise-independent-public-surface"));
            Assert.That(measurement.IsUnassessable, Is.True);
            Assert.That(measurement.Value, Is.Null);
            Assert.That(measurement.Contributors, Is.Null);
            Assert.That(measurement.ContributorCount, Is.Null);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.MissingRequiredInput }));
        });
    }

    [Test]
    public void Measure_CancellationMarksSnapshotTerminalAndRejectsFurtherEvaluation()
    {
        Fixture fixture = CreateFixture();
        using CancellationTokenSource cancellation = new();
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        string @namespace = "ArchLinterNet.Core.Model";
        ArchitectureContractDocument document = fixture.RunnerSetupService.DocumentToReturn;
        document.Topology = new ArchitectureTopology
        {
            Mode = "partial",
            SubjectKind = "type",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Namespace = @namespace }],
            },
            Nodes =
            [
                new ArchitectureTopologyNode
                {
                    Id = "model",
                    Mappings = [new ArchitectureTopologySubjectSelector { Namespace = @namespace }],
                },
            ],
        };
        document.Metrics =
        [
            new ArchitectureMetricDefinition
            {
                Id = "model-type-count",
                Kind = ArchitectureMetricKinds.TopologyTypeCount,
                TopologyNode = "model",
            },
        ];
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", [coreAssembly], Array.Empty<string>(), Array.Empty<string>())
        {
            CancellationToken = cancellation.Token,
        };
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null));
        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        cancellation.Cancel();

        Assert.Multiple(() =>
        {
            Assert.Throws<OperationCanceledException>(() => snapshot.Measure());
            Assert.That(snapshot.Cancelled, Is.True);
            Assert.Throws<OperationCanceledException>(() => snapshot.Measure());
            Assert.Throws<OperationCanceledException>(() => snapshot.Evaluate("strict"));
        });
    }

    [Test]
    public void EvaluateMeasureAndTopologyCapture_ReuseTheSnapshotPreparedRunner()
    {
        Fixture fixture = CreateFixture();
        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());

        snapshot.Evaluate("strict");
        snapshot.Measure();
        snapshot.CaptureTopologyObservation("type");
        snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(fixture.RunnerSetupService.BuildRunnerCallCount, Is.EqualTo(1));
            Assert.That(fixture.ContractExecutor.CallCountByMode.GetValueOrDefault("strict"), Is.EqualTo(1));
            Assert.That(fixture.ContractExecutor.CallCountByMode.GetValueOrDefault("audit"), Is.EqualTo(1));
            Assert.That(snapshot.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ModesEvaluated, Is.EqualTo(2));
            Assert.That(snapshot.Counters.SnapshotMaterializations, Is.EqualTo(1));
        });
    }
}
