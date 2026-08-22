using ArchLinterNet.Core.History.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryIngestionTextWriterTests
{
    [Test]
    public void RendersDeterministicMarkdownWithReportSectionsAndLimits()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Old.cs", "one\ntwo\n");
        string first = repository.Commit("first #5");
        repository.Move("src/Old.cs", "src/New.cs");
        string second = repository.Commit("rename #6");

        string markdown = HistoryIngestionTextWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.StartWith("# Release Architecture Forensics\n"));
            Assert.That(markdown, Does.Contain("## Analysis identity"));
            Assert.That(markdown, Does.Contain("## Hotspots"));
            Assert.That(markdown, Does.Contain("## Co-change clusters"));
            Assert.That(markdown, Does.Contain("## Parallel-development bottlenecks"));
            Assert.That(markdown, Does.Contain("## OCP pressure"));
            Assert.That(markdown, Does.Contain("## Refactoring candidates"));
            Assert.That(markdown, Does.Contain("## Enrichment"));
            Assert.That(markdown, Does.Contain("## Interpretation limits"));
            Assert.That(markdown, Does.Contain("Churn is change volume, not complexity."));
            Assert.That(markdown, Does.EndWith("\n"));
        });
    }

    [Test]
    public void RepresentsAnEmptyCandidateSetExplicitly()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        string markdown = HistoryIngestionTextWriter.Write(HistoryIngestionFixture.Succeed(repository, first, first));

        Assert.That(markdown, Does.Contain("No qualifying candidates."));
    }
}
