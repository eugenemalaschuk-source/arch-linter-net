using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureChangeSnapshotProjectorTests
{
    private static readonly string[] _violationEvidence = ["Acme.Service.Run: System.Console.WriteLine"];

    [Test]
    public void Project_CanonicalizesProjectIdentityAcrossCheckoutRoots()
    {
        ArchitectureChangeSnapshot linuxSnapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/home/agent/repository", "/home/agent/repository/src/Acme/Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());
        ArchitectureChangeSnapshot windowsSnapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("D:\\a\\repository", "D:\\a\\repository\\src\\Acme\\Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(linuxSnapshot, windowsSnapshot);

        Assert.Multiple(() =>
        {
            Assert.That(linuxSnapshot.Entries.Single().Identity, Is.EqualTo("src/Acme/Acme.csproj"));
            Assert.That(windowsSnapshot.Entries.Single().Identity, Is.EqualTo("src/Acme/Acme.csproj"));
            Assert.That(report.Added, Is.Empty);
            Assert.That(report.Removed, Is.Empty);
        });
    }

    [Test]
    public void Project_MapsGraphSemanticCoverageFindingAndBaselineFacts()
    {
        ArchitectureViolationIdentity identity = Identity(occurrence: 0);
        ArchitectureViolation violation = Violation(identity);
        ArchitectureClassificationRoleFact role = new(
            "Acme.Order",
            "aggregate",
            ArchitectureClassificationSource.Namespace,
            null,
            new Dictionary<string, object>
            {
                ["rank"] = 2.5m,
                ["bounded_context"] = "Sales",
            });
        ArchitectureCoverageSummary coverage = new(
            "coverage",
            "coverage-id",
            "namespace",
            new ArchitectureCoverageSummaryCounts(0, 0, 1, 1, 1),
            Array.Empty<ArchitectureCoverageSummaryExcludedItem>(),
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Uncovered", "evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Stale", "evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Unknown", "evidence") },
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>());
        ArchitectureGraphOutcome namespaceGraph = Graph(
            new ArchitectureGraphNode("Acme", ArchitectureGraphNodeKind.Namespace),
            new ArchitectureGraphEdge("Acme.Api", "Acme.Domain", ArchitectureGraphNodeKind.Namespace, ArchitectureGraphNodeKind.Namespace, Array.Empty<string>()));
        ArchitectureGraphOutcome assemblyGraph = Graph(
            new ArchitectureGraphNode("Acme", ArchitectureGraphNodeKind.Assembly),
            new ArchitectureGraphEdge("Acme.Api", "Acme.Domain", ArchitectureGraphNodeKind.Assembly, ArchitectureGraphNodeKind.Assembly, Array.Empty<string>()));
        string canonicalIdentity = ArchitectureFindingMapper.FromViolations(new[] { violation }, "strict").Single().CanonicalIdentity;

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", new[] { violation }, new[] { coverage }, new[] { role }),
            namespaceGraph,
            assemblyGraph,
            new[] { new ArchitectureBaselineComparisonEntry("group", "id", "Acme.Order", "forbidden", null, identity) },
            "ci");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ConditionSetName, Is.EqualTo("ci"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("src/Acme/Acme.csproj"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("namespace:Acme.Api->Acme.Domain"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("assembly:Acme.Api->Acme.Domain"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme.Order|aggregate|bounded_context=Sales;rank=2.5"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme.Order|bounded_context|Sales"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|uncovered|Acme.Uncovered"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|stale|Acme.Stale"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|unknown|Acme.Unknown"));
            Assert.That(snapshot.Findings.Single().Identity, Is.EqualTo(canonicalIdentity));
            Assert.That(snapshot.BaselineDebt, Is.EqualTo(new[] { canonicalIdentity }));
        });
    }

    [Test]
    public void Project_ExpandsAggregatedViolationIdentitiesBeforeComparingDrift()
    {
        ArchitectureViolationIdentity knownIdentity = Identity(occurrence: 0);
        ArchitectureViolationIdentity newIdentity = Identity(occurrence: 1);
        ArchitectureViolation aggregate = Violation(knownIdentity) with { Identities = new[] { knownIdentity, newIdentity } };
        string knownCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == knownIdentity.Occurrence)
            .CanonicalIdentity;
        string newCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == newIdentity.Occurrence)
            .CanonicalIdentity;
        ArchitectureChangeSnapshot current = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", new[] { aggregate }),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());
        ArchitectureChangeSnapshot baseline = new(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            string.Empty,
            Array.Empty<ArchitectureChangeEntry>(),
            new[] { current.Findings.Single(finding => finding.Identity == knownCanonicalIdentity) },
            Array.Empty<string>());

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(current.Findings, Has.Count.EqualTo(2));
            Assert.That(report.ExistingFindings, Has.Count.EqualTo(1));
            Assert.That(report.NewFindings, Has.Count.EqualTo(1));
            Assert.That(report.NewFindings[0].Identity, Is.EqualTo(newCanonicalIdentity));
        });
    }

    private static ArchitectureGraphOutcome EmptyGraph() => new(new ArchitectureDependencyGraph(
        Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>()));

    private static ArchitectureGraphOutcome Graph(ArchitectureGraphNode node, ArchitectureGraphEdge edge) => new(
        new ArchitectureDependencyGraph(new[] { node }, new[] { edge }));

    private static ArchitectureViolationIdentity Identity(int occurrence) => new(
        ArchitectureViolationIdentity.CurrentVersion,
        "method_body",
        "call",
        "forbidden-call",
        "Acme",
        "Acme.Service",
        "Run",
        "System.Console",
        "System.Console",
        "WriteLine",
        occurrence);

    private static ArchitectureViolation Violation(ArchitectureViolationIdentity identity) => new(
        "forbidden-call",
        "forbidden-call",
        "Acme.Service",
        "System.Console",
        _violationEvidence)
    {
        Identity = identity,
    };

    private static ValidationOutcome Outcome(
        string repositoryRoot,
        string projectPath,
        IReadOnlyCollection<ArchitectureViolation>? violations = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? roles = null) => new(
        Passed: true,
        Violations: violations ?? Array.Empty<ArchitectureViolation>(),
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: "off",
        PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: "off",
        CoverageSummaries: coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>(),
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = repositoryRoot,
            DiscoveredProjectPaths = new[] { projectPath },
            ClassificationRoles = roles ?? Array.Empty<ArchitectureClassificationRoleFact>(),
        };
}
