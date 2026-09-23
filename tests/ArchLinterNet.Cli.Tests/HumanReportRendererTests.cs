using ArchLinterNet.Cli.Commands.Validate.Application;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class HumanReportRendererTests
{
    [Test]
    public void RepositoryMetrics_AreRenderedAsSizeCouplingAndStructureGroups()
    {
        ValidationOutcome outcome = new(
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            "off",
            Array.Empty<PolicyConsistencyDiagnostic>(),
            "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryMetrics = new RepositoryMetricsSnapshot(
                RepositoryMetricsSnapshot.CurrentSchemaVersion,
                RepositoryMetricsSnapshot.CurrentKind,
                RepositoryMetricsAvailability.Complete,
                [],
                new RepositorySizeMetrics(100, 10, 3, 20, 5),
                new RepositoryCouplingMetrics(4, 4d / 3d, 1d / 3d, 2, 3, []),
                new RepositoryStructureMetrics(2, 1, 2, 2d / 3d, 2, 2d / 3d)),
        };

        string human = new HumanReportRenderer(null!).Render(true, [("strict", outcome)]);

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("Repository metrics:"));
            Assert.That(human, Does.Contain("  Size:"));
            Assert.That(human, Does.Contain("  Coupling:"));
            Assert.That(human, Does.Contain("  Structure:"));
            Assert.That(human, Does.Contain("    source lines: 100"));
            Assert.That(human, Does.Contain("    dependencies: 4"));
            Assert.That(human, Does.Contain("    maximum dependency depth: 2"));
            Assert.That(human, Does.Not.Contain("\n  source lines:"));
            Assert.That(human, Does.Not.Contain("\n  dependencies:"));
            Assert.That(human, Does.Not.Contain("\n  maximum dependency depth:"));
        });
    }
}
