using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
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

    [Test]
    public void Evaluate_PartiallyLoadableTypeUniverse_DoesNotBlockMetadataNativeAssemblyMetric()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create(includeUnloadableType: true);
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        ArchitectureTopologySubjectSelector selector = new() { Assembly = assemblyName };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-partial-types-assembly-topology",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "assembly",
                Scope = new ArchitectureTopologyScope { AllowEmpty = true, Selectors = [selector] },
                Nodes = [new ArchitectureTopologyNode { Id = "consumer", Mappings = [selector] }],
            },
            Metrics = [TopologyMetric("outgoing", ArchitectureMetricKinds.OutgoingComponentCount, "consumer")],
        };
        using ArchitectureAnalysisContext context = CreateContext(fixture.ConsumerAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        Assert.That(session.TypeIndex.HasCompleteTypeUniverse, Is.False);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.Zero);
            Assert.That(measurement.Contributors, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_PublicSurfaceWithAmbiguousSimpleAssemblyName_IsUnassessable()
    {
        const string AssemblyName = "MetricDuplicateAssembly";
        Assembly first = CreateDuplicateAssembly(AssemblyName, new Version(1, 0, 0, 0));
        Assembly second = CreateDuplicateAssembly(AssemblyName, new Version(2, 0, 0, 0));
        ArchitectureContractDocument document = new()
        {
            Name = "metric-ambiguous-public-surface",
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface =
                [
                    new ArchitecturePublicApiSurfaceContract
                    {
                        Id = "surface",
                        Name = "Surface",
                        Assemblies = [AssemblyName],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "surface-size",
                    Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                    PublicApiSurface = "surface",
                },
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(first, second);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_PublicSurfaceWithPartialExportedTypeUniverse_IsUnassessable()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create(includeUnloadableType: true);
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-partial-public-surface",
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface =
                [
                    new ArchitecturePublicApiSurfaceContract
                    {
                        Id = "surface",
                        Name = "Surface",
                        Assemblies = [assemblyName],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "surface-size",
                    Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                    PublicApiSurface = "surface",
                },
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(fixture.ConsumerAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_PublicSurfaceWithIncompleteMemberSignature_IsUnassessable()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create();
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-incomplete-public-member-surface",
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface =
                [
                    new ArchitecturePublicApiSurfaceContract
                    {
                        Id = "surface",
                        Name = "Surface",
                        Assemblies = [assemblyName],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "surface-size",
                    Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                    PublicApiSurface = "surface",
                },
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(fixture.ConsumerAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    private static Assembly CreateDuplicateAssembly(string simpleName, Version version)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(simpleName) { Version = version }, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Main");
        _ = module.DefineType("PublicType", TypeAttributes.Public).CreateType();
        return assembly;
    }
}
