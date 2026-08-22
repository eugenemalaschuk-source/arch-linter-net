using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryOcpScorerTests
{
    [Test]
    public void CanonicalTaskKeysAndPairExclusiveCommitsProduceDeduplicatedRepeatedEditEvidence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("OrderService.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("OrderService.cs", "two\n");
        repository.Commit("first #001");
        repository.Write("OrderService.cs", "three\n");
        repository.Commit("second #1");
        repository.Write("OrderService.cs", "four\n");
        repository.Commit("shared #1 #2");
        repository.Write("OrderService.cs", "five\n");
        string last = repository.Commit("other #2");

        HistoryOcpFinding finding = HistoryIngestionFixture.Succeed(repository, first, last).OcpAnalysis.Findings.Single();
        OcpTaskRepeatedEdit repeated = finding.RawEvidence.RepeatedEdits.Single(static item => item.TaskKey.ToString() == "issue#1");

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(new[] { "issue#1", "issue#2" }));
            Assert.That(repeated.QualifyingCommitIds, Has.Count.EqualTo(2));
            Assert.That(repeated.RepeatedEditCount, Is.EqualTo(1));
            Assert.That(finding.RawEvidence.RepeatedEditTotal, Is.EqualTo(1));
            Assert.That(finding.RawEvidence.RoleTokens, Is.EqualTo(new[] { "service" }));
            Assert.That(finding.RawEvidence.RoleHint, Is.EqualTo(1.000000000m));
        });
    }

    [Test]
    public void OneTaskWithMultiplePartnersCountsEachQualifyingShaOnlyOnce()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        repository.Commit("first #1");
        repository.Write("X.cs", "three\n");
        repository.Commit("second #1");
        repository.Write("X.cs", "four\n");
        repository.Commit("partner two #2");
        repository.Write("X.cs", "five\n");
        string last = repository.Commit("partner three #3");

        HistoryOcpFinding finding = HistoryIngestionFixture.Succeed(repository, first, last).OcpAnalysis.Findings.Single();
        OcpTaskRepeatedEdit repeated = finding.RawEvidence.RepeatedEdits.Single(static item => item.TaskKey.ToString() == "issue#1");

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.IndependentTaskPairs, Has.Count.EqualTo(3));
            Assert.That(repeated.QualifyingCommitIds, Has.Count.EqualTo(2));
            Assert.That(repeated.RepeatedEditCount, Is.EqualTo(1));
            Assert.That(finding.RawEvidence.RepeatedEditTotal, Is.EqualTo(1));
        });
    }

    [Test]
    public void OneMultiReferenceCommitCannotCreateIndependentOrRepeatedEditPressure()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        string last = repository.Commit("shared #101 #102");

        HistoryOcpFinding finding = HistoryIngestionFixture.Succeed(repository, first, last).OcpAnalysis.Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.IndependentTaskSpread, Is.Zero);
            Assert.That(finding.RawEvidence.IndependentTaskPairs, Is.Empty);
            Assert.That(finding.RawEvidence.RepeatedEdits, Is.Empty);
            Assert.That(finding.RawEvidence.RepeatedEditTotal, Is.Zero);
        });
    }

    [Test]
    public void NamespaceDistinctTaskKeysRemainDistinctInOcpEvidence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        repository.Commit("default #1");
        repository.Write("X.cs", "three\n");
        string last = repository.Commit("configured JIRA-1");
        var configuration = new HistoryAnalysisConfiguration
        {
            Extractors = [new HistoryTaskExtractorConfiguration
            {
                Id = "jira",
                Namespace = "jira",
                Pattern = new HistoryTaskExtractorPattern { Prefix = "JIRA-" },
            }],
        };

        HistoryOcpFinding finding = HistoryIngestionFixture.Succeed(repository, first, last, configuration).OcpAnalysis.Findings.Single();

        Assert.That(finding.RawEvidence.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(new[] { "issue#1", "jira#1" }));
    }

    [Test]
    public void RoleTokensUseExactPortableAsciiTokenization()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoryOcpScorer.RoleTokens("OrderService.cs"), Is.EqualTo(new[] { "service" }));
            Assert.That(HistoryOcpScorer.RoleTokens("DiagnosticMapper.cs"), Is.EqualTo(new[] { "diagnostic", "mapper" }));
            Assert.That(HistoryOcpScorer.RoleTokens("ViewModel.cs"), Is.EqualTo(new[] { "model" }));
            Assert.That(HistoryOcpScorer.RoleTokens("XMLParser2.cs"), Is.Empty);
            Assert.That(HistoryOcpScorer.RoleTokens("Serviceable.cs"), Is.Empty);
            Assert.That(HistoryOcpScorer.RoleTokens("MyDispatcherFactory.cs"), Is.EqualTo(new[] { "dispatcher" }));
        });
    }

    [Test]
    public void CoChangeThresholdCannotChangeOcpCentralityOrScore()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Write("B.cs", "two\n");
        repository.Commit("together #1");
        repository.Write("A.cs", "three\n");
        string last = repository.Commit("separate #2");

        HistoryOcpAnalysis low = HistoryIngestionFixture.Succeed(repository, first, last, Configuration(0m)).OcpAnalysis;
        HistoryOcpAnalysis high = HistoryIngestionFixture.Succeed(repository, first, last, Configuration(1m)).OcpAnalysis;

        Assert.That(high.Findings.Select(Describe), Is.EqualTo(low.Findings.Select(Describe)));
    }

    [Test]
    public void EqualScoresUseOrdinaryTaskSpreadChurnAndCommitCountBeforePath()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "base\n");
        repository.Write("B.cs", "base\n");
        repository.Write("C.cs", "base\n");
        repository.Write("D.cs", "base\n");
        string first = repository.Commit("base");

        repository.Write("A.cs", "base\na\n");
        repository.Commit("a #1");
        repository.Write("B.cs", "base\nb\n");
        repository.Commit("b #1");
        repository.Write("B.cs", "base\nb\nc\n");
        repository.Commit("b #2");
        repository.Write("C.cs", "base\nc\nd\n");
        repository.Commit("c #1");
        repository.Write("C.cs", "base\nc\nd\ne\n");
        repository.Commit("c #2");
        repository.Write("D.cs", "base\nd\n");
        repository.Commit("d #1");
        repository.Write("D.cs", "base\nd\ne\n");
        repository.Commit("d #2");
        repository.Write("D.cs", "base\nd\ne\nf\n");
        string last = repository.Commit("d #1");

        var configuration = new HistoryAnalysisConfiguration
        {
            Weights = new HistoryAnalysisWeightProfiles
            {
                Ocp = new HistoryOcpWeightProfile
                {
                    IndependentTask = 0m,
                    Centrality = 0m,
                    RepeatedEdit = 0m,
                    RoleHint = 1m,
                },
            },
        };

        HistoryOcpAnalysis analysis = HistoryIngestionFixture.Succeed(repository, first, last, configuration).OcpAnalysis;

        Assert.That(analysis.Findings.Select(static finding => finding.CanonicalPath), Is.EqualTo(new[] { "D.cs", "C.cs", "B.cs", "A.cs" }));
    }

    [Test]
    public void IgnoredLogicalFilesDoNotProduceOcpFindings()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("ignored/X.cs", "base\n");
        repository.Write("Y.cs", "base\n");
        string first = repository.Commit("base");
        repository.Write("ignored/X.cs", "changed\n");
        repository.Write("Y.cs", "changed\n");
        string last = repository.Commit("change #1");
        var configuration = new HistoryAnalysisConfiguration { Ignore = ["ignored/**"] };

        HistoryOcpAnalysis analysis = HistoryIngestionFixture.Succeed(repository, first, last, configuration).OcpAnalysis;

        Assert.That(analysis.Findings.Select(static finding => finding.CanonicalPath), Is.EqualTo(new[] { "Y.cs" }));
    }

    [Test]
    public void JsonExposesOcpEvidenceAndPathnameReuseCaveat()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/OrderService.cs", "one\n");
        string first = repository.Commit("base");
        repository.Remove("src/OrderService.cs");
        repository.Commit("delete #1");
        repository.Write("src/OrderService.cs", "unrelated\n");
        string last = repository.Commit("readd #2");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, last));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"ocpGroups\""));
            Assert.That(json, Does.Contain("\"repeatedEdits\""));
            Assert.That(json, Does.Contain("\"roleTokens\""));
            Assert.That(json, Does.Contain("\"pathnameReuseMayConflateGenerations\": true"));
        });
    }

    private static HistoryAnalysisConfiguration Configuration(decimal threshold) => new()
    {
        Thresholds = new HistoryAnalysisThresholds { CoChangeSignificance = threshold },
    };

    private static string Describe(HistoryOcpFinding finding)
        => $"{finding.CanonicalPath}:{finding.Components.Centrality:F9}:{finding.Score:F9}";
}
