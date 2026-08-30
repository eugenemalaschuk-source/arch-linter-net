using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureMetricApplicabilityTests
{
    [Test]
    public void Evaluate_ProjectTopologyWithDistinctArtifactsSharingOutputName_IsUnassessableWithoutMergingOwners()
    {
        Assembly firstAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        Assembly secondAssembly = typeof(ArchitectureMetricApplicabilityTests).Assembly;
        string firstArtifact = Path.Combine(Path.GetTempPath(), "metric-first", "Shared.dll");
        string secondArtifact = Path.Combine(Path.GetTempPath(), "metric-second", "Shared.dll");
        const string OutputAssemblyName = "Shared";
        ArchitectureContractDocument document = CreateProjectFootprintDocument(OutputAssemblyName);
        ProjectDiscoveryResult discovery = new(
            [firstAssembly.GetName().Name!, secondAssembly.GetName().Name!],
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("src/First/First.csproj", OutputAssemblyName, ["net10.0"]),
                new ArchitectureDiscoveredProject("src/Second/Second.csproj", OutputAssemblyName, ["net10.0"]),
            ],
            ResolvedAssemblyPathsByNormalizedProjectPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["src/First/First.csproj"] = firstArtifact,
                ["src/Second/Second.csproj"] = secondArtifact,
            },
        };
        using ArchitectureAnalysisContext context = new(
            Path.GetTempPath(),
            [firstAssembly, secondAssembly],
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery)
        {
            ResolvedAssemblyArtifactPaths = new Dictionary<Assembly, string>
            {
                [firstAssembly] = firstArtifact,
                [secondAssembly] = secondArtifact,
            },
        };

        ArchitectureMetricMeasurementOutcome outcome = Measure(context, document);
        ArchitectureMetricMeasurement measurement = outcome.Measurements.Single();
        ArchitectureApplicabilityRecord record = outcome.Applicability!.Controls.Single().Record!;

        Assert.Multiple(() =>
        {
            AssertUnassessable(measurement);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.MissingRequiredInput }));
        });
    }

    [Test]
    public void Evaluate_ProjectTopologyWithOneExactArtifactOwner_UsesTheCanonicalProjectContributor()
    {
        Assembly assembly = typeof(ArchitectureMetricMeasurement).Assembly;
        string assemblyName = assembly.GetName().Name!;
        const string ProjectPath = "src/Core/Core.csproj";
        ProjectDiscoveryResult discovery = new(
            [assemblyName], Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = [new ArchitectureDiscoveredProject(ProjectPath, assemblyName, ["net10.0"])],
            ResolvedAssemblyPathsByNormalizedProjectPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectPath] = assembly.Location,
            },
        };
        using ArchitectureAnalysisContext context = new(
            Path.GetTempPath(), [assembly], Array.Empty<string>(), Array.Empty<string>(), projectDiscovery: discovery);

        ArchitectureMetricMeasurement measurement = Measure(context, CreateProjectFootprintDocument(assemblyName))
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.EqualTo(1));
            Assert.That(measurement.Contributors, Is.EqualTo(new[] { ProjectPath }));
        });
    }

    private static ArchitectureMetricMeasurementOutcome Measure(
        ArchitectureAnalysisContext context,
        ArchitectureContractDocument document)
    {
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);
        return ArchitectureMetricEvaluator.Evaluate(session, document.Metrics);
    }

    private static ArchitectureContractDocument CreateProjectFootprintDocument(string projectSelector) => new()
    {
        Name = "metric-project-topology",
        Topology = new ArchitectureTopology
        {
            Mode = "partial",
            SubjectKind = "project",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Project = projectSelector }],
            },
            Nodes = [ProjectNode("selected", projectSelector)],
        },
        Metrics =
        [
            new ArchitectureMetricDefinition
            {
                Id = "project-footprint",
                Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                TopologyNode = "selected",
                Unit = "project",
            },
        ],
    };
}
