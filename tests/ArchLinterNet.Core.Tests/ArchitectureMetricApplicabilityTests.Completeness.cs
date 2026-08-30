using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureMetricApplicabilityTests
{
    [Test]
    public void Evaluate_IncompleteDirectReferenceEvidence_IsUnassessableWithoutPartialMetricValues()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create();
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        ArchitectureTopologySubjectSelector selector = new() { Project = assemblyName };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-incomplete-reference-evidence",
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>(StringComparer.Ordinal)
            {
                ["vendor-sdk"] = new ArchitectureExternalDependencyGroup
                {
                    NamespacePrefixes = ["VendorSdk"],
                },
            },
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "project",
                Scope = new ArchitectureTopologyScope { Selectors = [selector] },
                Nodes = [new ArchitectureTopologyNode { Id = "consumer", Mappings = [selector] }],
            },
            Metrics =
            [
                TopologyMetric("incoming", ArchitectureMetricKinds.IncomingComponentCount, "consumer"),
                TopologyMetric("outgoing", ArchitectureMetricKinds.OutgoingComponentCount, "consumer"),
                TopologyMetric("external", ArchitectureMetricKinds.ExternalDependencyGroupCount, "consumer"),
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(fixture.ConsumerAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Measurements, Has.Count.EqualTo(3));
            Assert.That(outcome.Measurements.All(measurement =>
                    measurement.IsUnassessable
                    && measurement.Value == null
                    && measurement.Contributors.Count == 0),
                Is.True);
            Assert.That(outcome.Applicability!.Controls.Select(control => control.Record!).SelectMany(record => record.Reasons)
                    .Select(reason => reason.Code),
                Is.All.EqualTo(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
        });
    }

    [Test]
    public void Evaluate_PartiallyLoadableTypeUniverse_IsUnassessableWithoutTrustedZero()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create(includeUnloadableType: true);
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        ArchitectureTopologySubjectSelector selector = new() { Project = assemblyName };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-partially-loadable-type-universe",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "project",
                Scope = new ArchitectureTopologyScope { AllowEmpty = true, Selectors = [selector] },
                Nodes = [new ArchitectureTopologyNode { Id = "consumer", Mappings = [selector] }],
            },
            Metrics = [TopologyMetric("type-count", ArchitectureMetricKinds.TopologyTypeCount, "consumer")],
        };
        using ArchitectureAnalysisContext context = CreateContext(fixture.ConsumerAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(session.TypeIndex.HasCompleteTypeUniverse, Is.False);
            AssertUnassessable(measurement);
        });
    }
}
