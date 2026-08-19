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

    private static ArchitectureGraphOutcome EmptyGraph() => new(new ArchitectureDependencyGraph(
        Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>()));

    private static ValidationOutcome Outcome(string repositoryRoot, string projectPath) => new(
        Passed: true,
        Violations: Array.Empty<ArchitectureViolation>(),
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
