using System.Numerics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryHotspotScorerTests
{
    [Test]
    public void ScoresCanonicalEvidenceWithinItsOwnCategoryAndRanksByScore()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/A.cs", "one\n");
        repository.Write("src/B.cs", "one\n");
        repository.Write("generated/Large.cs", "one\n");
        string from = repository.Commit("baseline");

        repository.Write("src/A.cs", "one\ntwo\n");
        repository.Write("src/B.cs", "one\ntwo\n");
        repository.Write("generated/Large.cs", string.Concat(Enumerable.Repeat("line\n", 100)));
        repository.Commit("implement #001");
        repository.Write("src/A.cs", "one\ntwo\nthree\n");
        string to = repository.Commit("extend #1");

        HistoryHotspotAnalysis analysis = Score(repository, from, to, configuration =>
        {
            configuration.Paths.Production.Add("src/**");
            configuration.Paths.Generated.Add("generated/**");
        });
        HotspotCategoryGroup production = analysis.Groups.Single(group => group.Category == HistoryPathCategory.Production);
        HotspotFinding first = production.Findings[0];
        HotspotFinding second = production.Findings[1];

        Assert.Multiple(() =>
        {
            Assert.That(first.CanonicalPath, Is.EqualTo("src/A.cs"));
            Assert.That(first.RawEvidence.CommitCount, Is.EqualTo(2));
            Assert.That(first.RawEvidence.TaskSpread, Is.EqualTo(1), "#001 and #1 are one canonical TaskKey");
            Assert.That(first.RawEvidence.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(["issue#1"]));
            Assert.That(first.RawEvidence.TaskKeyProvenance.Select(static item => item.Match.MatchedText), Is.EqualTo(["#001", "#1"]));
            Assert.That(first.RawEvidence.CanonicalAuthors, Is.EqualTo(["fixture@example.com"]));
            Assert.That(first.RawEvidence.AuthorProvenance, Has.Count.EqualTo(2));
            Assert.That(first.RawEvidence.TemporalSpanSeconds, Is.EqualTo(new BigInteger(3600)));
            Assert.That(first.Components.Churn, Is.EqualTo(1m), "generated churn cannot set the production maximum");
            Assert.That(second.CanonicalPath, Is.EqualTo("src/B.cs"));
            Assert.That(second.Components.Churn, Is.EqualTo(0.630929754m), "Q(log(1 + 1) / log(1 + 2))");
            Assert.That(second.PathEvents, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void RetainsMissingTaskAndBinaryEvidenceWithoutReweighting()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string from = repository.Commit("baseline");
        repository.WriteBytes("src/Binary.dat", [0x41, 0x00, 0x42]);
        string to = repository.Commit("binary change without task");

        HotspotFinding finding = Score(repository, from, to, configuration => configuration.Paths.Production.Add("src/**"))
            .GetFindings().Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.Churn, Is.Zero);
            Assert.That(finding.RawEvidence.HasBinaryOrUnavailableEvidence, Is.True);
            Assert.That(finding.RawEvidence.TaskSpread, Is.Zero);
            Assert.That(finding.Components.Task, Is.Zero);
            Assert.That(finding.Score, Is.EqualTo(0.400000000m), "missing task evidence leaves default weights intact");
        });
    }

    [Test]
    public void GivesAcceptedExactRenameOneZeroChurnTouch()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Old.cs", "contents\n");
        string from = repository.Commit("baseline");
        repository.Move("src/Old.cs", "src/New.cs");
        string to = repository.Commit("rename");

        HotspotFinding finding = Score(repository, from, to, configuration => configuration.Paths.Production.Add("src/**"))
            .GetFindings().Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.CanonicalPath, Is.EqualTo("src/New.cs"));
            Assert.That(finding.RawEvidence.CommitCount, Is.EqualTo(1));
            Assert.That(finding.RawEvidence.Churn, Is.Zero);
            Assert.That(finding.RawEvidence.HasExactRenameEvidence, Is.True);
            Assert.That(finding.RawEvidence.PathnameReuseMayConflateGenerations, Is.True);
        });
    }

    private static HistoryHotspotAnalysis Score(
        GitTestRepository repository,
        string from,
        string to,
        Action<HistoryAnalysisConfiguration> configure)
    {
        var configuration = new HistoryAnalysisConfiguration();
        configure(configuration);
        return new HistoryHotspotScorer().Score(HistoryIngestionFixture.Succeed(repository, from, to), configuration);
    }
}
