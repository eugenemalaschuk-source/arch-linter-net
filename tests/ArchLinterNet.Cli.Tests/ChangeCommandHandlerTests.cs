using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ChangeCommandHandlerTests
{
    [Test]
    public void BuildSnapshot_CanonicalizesProjectIdentityAcrossCheckoutRoots()
    {
        ArchitectureChangeSnapshot linuxSnapshot = ChangeCommandHandler.BuildSnapshot(
            "strict",
            Outcome("/home/agent/repository", "/home/agent/repository/src/Acme/Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<string>());
        ArchitectureChangeSnapshot windowsSnapshot = ChangeCommandHandler.BuildSnapshot(
            "strict",
            Outcome("D:\\a\\repository", "D:\\a\\repository\\src\\Acme\\Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<string>());

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
    public void BuildSnapshot_ExpandsAggregatedViolationIdentitiesBeforeComparingDrift()
    {
        ArchitectureViolationIdentity knownIdentity = Identity(occurrence: 0);
        ArchitectureViolationIdentity newIdentity = Identity(occurrence: 1);
        ArchitectureViolation aggregate = new(
            "forbidden-call",
            "forbidden-call",
            "Acme.Service",
            "System.Console",
            new[] { "Acme.Service.Run: System.Console.WriteLine" })
        {
            Identity = knownIdentity,
            Identities = new[] { knownIdentity, newIdentity },
        };
        string knownCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == knownIdentity.Occurrence)
            .CanonicalIdentity;
        string newCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == newIdentity.Occurrence)
            .CanonicalIdentity;
        ArchitectureChangeSnapshot current = ChangeCommandHandler.BuildSnapshot(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", aggregate),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<string>());
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

    [Test]
    public void OutputCollisionGuards_RejectEveryChangeCommandInput()
    {
        ChangeSnapshotCommandOptions snapshotWithPolicyCollision = new(
            "policy.yml", "strict", null, "baseline.yml", "policy.yml", false);
        ChangeSnapshotCommandOptions snapshotWithBaselineCollision = new(
            "policy.yml", "strict", null, "baseline.yml", "baseline.yml", false);
        ChangeReportCommandOptions reportWithBaseCollision = new(
            "base.json", "current.json", "json", "base.json", false);
        ChangeReportCommandOptions reportWithCurrentCollision = new(
            "base.json", "current.json", "json", "current.json", false);

        Assert.Multiple(() =>
        {
            Assert.That(ChangeCommandHandler.FindSnapshotOutputCollision(snapshotWithPolicyCollision), Does.Contain("--policy"));
            Assert.That(ChangeCommandHandler.FindSnapshotOutputCollision(snapshotWithBaselineCollision), Does.Contain("--baseline"));
            Assert.That(ChangeCommandHandler.FindReportOutputCollision(reportWithBaseCollision), Does.Contain("--base"));
            Assert.That(ChangeCommandHandler.FindReportOutputCollision(reportWithCurrentCollision), Does.Contain("--current"));
        });
    }

    private static ArchitectureGraphOutcome EmptyGraph() => new(new ArchitectureDependencyGraph(
        Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>()));

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

    private static ValidationOutcome Outcome(
        string repositoryRoot,
        string projectPath,
        params ArchitectureViolation[] violations) => new(
        Passed: true,
        Violations: violations,
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: "off",
        PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: "off",
        CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = repositoryRoot,
            DiscoveredProjectPaths = new[] { projectPath },
        };
}
