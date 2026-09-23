using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class PrReportMarkdownRendererTests
{
    [Test]
    public void RepositoryMetricsDelta_OmitsUnchangedRowsByDefault()
    {
        RepositoryMetricsDelta delta = new(
            RepositoryMetricsDelta.CurrentSchemaVersion,
            RepositoryMetricsDelta.CurrentKind,
            RepositoryMetricsDeltaAvailability.Complete,
            [],
            [
                new("source_files", "count", 10, 10, 0),
                new("source_lines", "count", 100, 125, 25),
            ]);
        ArchitecturePrReportChange change = Change() with { RepositoryMetricsDelta = delta };

        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(change: change));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Changed base → head metrics (1)"));
            Assert.That(markdown, Does.Contain("source_lines: 100 → 125 (delta 25)"));
            Assert.That(markdown, Does.Contain("Unchanged metrics: 1 omitted by default."));
            Assert.That(markdown, Does.Not.Contain("source_files: 10 → 10"));
        });
    }
}
