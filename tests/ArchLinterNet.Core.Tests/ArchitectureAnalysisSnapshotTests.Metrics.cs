using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureAnalysisSnapshotTests
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
            Assert.That(measurement.Contributors, Is.Empty);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.MissingRequiredInput }));
        });
    }
}
