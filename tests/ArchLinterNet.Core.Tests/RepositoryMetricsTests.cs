using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RepositoryMetricsTests
{
    [Test]
    public void Calculator_ProjectsCycleAndCouplingFromDiscoveredProjectGraph()
    {
        ArchitectureDiscoveredProject first = Project("src/First/First.csproj", "First", "src/Second/Second.csproj");
        ArchitectureDiscoveredProject second = Project("src/Second/Second.csproj", "Second", "src/Third/Third.csproj");
        ArchitectureDiscoveredProject third = Project("src/Third/Third.csproj", "Third", "src/First/First.csproj");
        ProjectDiscoveryResult discovery = new([], [], [], [])
        {
            DiscoveredProjects = [first, second, third],
        };
        using ArchitectureAnalysisContext context = new(
            "/repo",
            [typeof(RepositoryMetricsTests).Assembly],
            [],
            [],
            projectDiscovery: discovery);
        ArchitectureAnalysisSession session = new(
            context,
            new ArchitectureContractDocument { Name = "repository-metrics" },
            null,
            false,
            null);

        RepositoryMetricsSnapshot metrics = session.GetRepositoryMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(metrics.IsComplete, Is.True);
            Assert.That(metrics.Size.Projects, Is.EqualTo(3));
            Assert.That(metrics.Coupling.DependencyCount, Is.EqualTo(3));
            Assert.That(metrics.Coupling.DependencyDensity, Is.EqualTo(0.5d));
            Assert.That(metrics.Structure.CyclicComponentCount, Is.EqualTo(1));
            Assert.That(metrics.Structure.CyclicProjectCount, Is.EqualTo(3));
            Assert.That(metrics.Structure.MaxDependencyDepth, Is.EqualTo(0));
            Assert.That(metrics.Structure.LargestSccSize, Is.EqualTo(3));
            Assert.That(metrics.Coupling.Projects.Select(project => project.Instability), Is.All.EqualTo(0.5d));
        });
    }

    [Test]
    public void Calculator_DeduplicatesEdgesAndProjectsDiamondDepth()
    {
        ProjectDiscoveryResult discovery = new([], [], [], [])
        {
            DiscoveredProjects =
            [
                Project("src/A/A.csproj", "A", "src/B/B.csproj", "src/B/B.csproj", "src/C/C.csproj"),
                Project("src/B/B.csproj", "B", "src/D/D.csproj"),
                Project("src/C/C.csproj", "C", "src/D/D.csproj"),
                Project("src/D/D.csproj", "D"),
            ],
        };

        RepositoryMetricsSnapshot metrics = CreateSession(discovery).GetRepositoryMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(metrics.Coupling.DependencyCount, Is.EqualTo(4));
            Assert.That(metrics.Coupling.DependencyDensity, Is.EqualTo(4d / 12d));
            Assert.That(metrics.Structure.MaxDependencyDepth, Is.EqualTo(2));
            Assert.That(metrics.Structure.CyclicComponentCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculator_CountsSelfLoopAndIsolatedProjectAsCycleAndZeroDepth()
    {
        ProjectDiscoveryResult discovery = new([], [], [], [])
        {
            DiscoveredProjects =
            [
                Project("src/Loop/Loop.csproj", "Loop", "src/Loop/Loop.csproj"),
                Project("src/Isolated/Isolated.csproj", "Isolated"),
            ],
        };

        RepositoryMetricsSnapshot metrics = CreateSession(discovery).GetRepositoryMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(metrics.Coupling.DependencyCount, Is.EqualTo(1));
            Assert.That(metrics.Coupling.DependencyDensity, Is.EqualTo(0d));
            Assert.That(metrics.Structure.CyclicComponentCount, Is.EqualTo(1));
            Assert.That(metrics.Structure.CyclicProjectCount, Is.EqualTo(1));
            Assert.That(metrics.Structure.CyclicProjectRatio, Is.EqualTo(0.5d));
            Assert.That(metrics.Structure.MaxDependencyDepth, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculator_UsesZeroRatiosForEmptyProjectGraph()
    {
        RepositoryMetricsSnapshot metrics = CreateSession(new ProjectDiscoveryResult([], [], [], [])).GetRepositoryMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(metrics.Size.Projects, Is.EqualTo(0));
            Assert.That(metrics.Coupling.DependencyCount, Is.EqualTo(0));
            Assert.That(metrics.Coupling.DependenciesPerProject, Is.EqualTo(0d));
            Assert.That(metrics.Coupling.DependencyDensity, Is.EqualTo(0d));
            Assert.That(metrics.Structure.LargestSccSize, Is.EqualTo(0));
            Assert.That(metrics.Structure.LargestSccRatio, Is.EqualTo(0d));
        });
    }

    [Test]
    public void Calculator_DoesNotMaterializeConfiguredSourceFactsWithoutExplicitRequest()
    {
        using ArchitectureAnalysisContext context = new(
            "/repo",
            [typeof(RepositoryMetricsTests).Assembly],
            [],
            []);
        ArchitectureAnalysisSession session = new(
            context,
            new ArchitectureContractDocument
            {
                Name = "repository-metrics",
                Analysis = new ArchitectureAnalysisConfiguration { SourceRoots = ["src"] },
            },
            null,
            false,
            null);

        Assert.That(session.SourceFileFactIndex.IsMaterialized, Is.False);

        RepositoryMetricsSnapshot cheap = session.GetRepositoryMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(session.SourceFileFactIndex.IsMaterialized, Is.False);
            Assert.That(cheap.Size.SourceLines, Is.Null);
            Assert.That(cheap.ReasonCodes, Does.Contain(RepositoryMetricsReasonCodes.SourceInventoryNotMaterialized));
        });

        session.GetRepositoryMetrics(includeSourceInventory: true);

        Assert.That(session.SourceFileFactIndex.IsMaterialized, Is.True);
    }

    [Test]
    public void DeltaFactory_ReportsTypedUnavailableEvidenceAndCompleteValues()
    {
        RepositoryMetricsSnapshot baseline = Snapshot(100);
        RepositoryMetricsSnapshot current = Snapshot(125);

        RepositoryMetricsDelta delta = RepositoryMetricsDeltaFactory.Create(baseline, current);
        RepositoryMetricsDelta unavailable = RepositoryMetricsDeltaFactory.Create(null, current);

        Assert.Multiple(() =>
        {
            Assert.That(delta.IsComplete, Is.True);
            Assert.That(delta.Metrics.Single(metric => metric.Name == "source_lines").Delta, Is.EqualTo(25d));
            Assert.That(unavailable.IsComplete, Is.False);
            Assert.That(unavailable.ReasonCodes, Does.Contain("missing_base_or_head_metrics"));
        });
    }

    [Test]
    public void JsonRoundTrip_UsesStableVersionedRepositoryMetricsShape()
    {
        RepositoryMetricsSnapshot snapshot = Snapshot(100);

        RepositoryMetricsSnapshot parsed = RepositoryMetricsJson.Deserialize(RepositoryMetricsJson.Serialize(snapshot));

        Assert.That(parsed, Is.EqualTo(snapshot));
    }

    private static ArchitectureDiscoveredProject Project(
        string path,
        string assemblyName,
        params string[] referencePaths) =>
        new(path, assemblyName, ["net10.0"])
        {
            ProjectReferences = referencePaths
                .Select(referencePath => new ArchitectureDiscoveredProjectReference(referencePath, path))
                .ToArray(),
        };

    private static ArchitectureAnalysisSession CreateSession(ProjectDiscoveryResult discovery)
    {
        ArchitectureAnalysisContext context = new(
            "/repo",
            [typeof(RepositoryMetricsTests).Assembly],
            [],
            [],
            projectDiscovery: discovery);
        return new ArchitectureAnalysisSession(
            context,
            new ArchitectureContractDocument { Name = "repository-metrics" },
            null,
            false,
            null);
    }

    private static RepositoryMetricsSnapshot Snapshot(int sourceLines) =>
        new(
            RepositoryMetricsSnapshot.CurrentSchemaVersion,
            RepositoryMetricsSnapshot.CurrentKind,
            RepositoryMetricsAvailability.Complete,
            [],
            new RepositorySizeMetrics(sourceLines, 10, 3, 20, 5),
            new RepositoryCouplingMetrics(2, 2d / 3d, 1d / 3d, 1, 1, []),
            new RepositoryStructureMetrics(2, 0, 0, 0d, 1, 1d / 3d));
}
