using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryCanonicalJsonTests
{
    [Test]
    public void CanonicalBytesUseLfTwoSpaceIndentationAndOneTerminalLf()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.That(json, Does.Not.Contain("\r"));
        Assert.That(json, Does.EndWith("}\n"));
        Assert.That(json[..^1], Does.Not.EndWith("\n"));
        Assert.That(json.Split('\n'), Has.None.Matches<string>(static line => line.Length > 0 && line.TrimEnd() != line));
        Assert.That(json, Does.Contain("\n  \"schemaVersion\": 1"));
        Assert.That(json, Does.Contain("\n  \"analysis\": {\n    \"objectFormat\": \"sha1\""));
    }

    [Test]
    public void RepeatedRunsOverIdenticalObjectsProduceIdenticalBytes()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Move("a.txt", "b.txt");
        string second = repository.Commit("rename for #3 and #4");

        string firstRun = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));
        string secondRun = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.That(secondRun, Is.EqualTo(firstRun));
    }

    [Test]
    public void EscapingFollowsTheCanonicalProfile()
    {
        string quoted = CanonicalJsonWriter.Quote("a\"b\\c/d\u0001e\nf\u00E9");

        Assert.That(quoted, Is.EqualTo("\"a\\\"b\\\\c/d\\u0001e\\nfé\""));
    }

    [Test]
    public void AnEmptyRangeSerializesExplicitEmptyEvidence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, first));

        Assert.That(json, Does.Contain("\"commits\": []"));
        Assert.That(json, Does.Contain("\"logicalFiles\": []"));
        Assert.That(json, Does.Contain("\"analyzedCommitCount\": 0"));
        Assert.That(json, Does.Contain("\"excludedMergeCount\": 0"));
        Assert.That(json, Does.Contain("\"candidates\": []"));
    }

    [Test]
    public void ArbitraryPrecisionIntegersAreWrittenWithoutExponentOrTruncation()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        string second = repository.Commit("closes #123456789012345678901234567890");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.That(json, Does.Contain("\"id\": 123456789012345678901234567890"));
    }

    [Test]
    public void DiagnosticsSerializeSeparatelyFromIngestionResults()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, "HEAD~2");
        string json = HistoryDiagnosticJsonWriter.Write(diagnostic);

        Assert.That(json, Does.StartWith("{\n  \"kind\": \"ref_unresolved\""));
        Assert.That(json, Does.EndWith("}\n"));
        Assert.That(json, Does.Not.Contain("logicalFiles"));
    }

    [Test]
    public void SuccessfulReportCarriesCanonicalConfigurationEnrichmentAndCandidates()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"historyAnalysisConfiguration\""));
            Assert.That(json, Does.Contain("\"hotspotGroups\""));
            Assert.That(json, Does.Contain("\"status\": \"not_requested\""));
            Assert.That(json, Does.Contain("\"kind\": \"hotspot\""));
            Assert.That(json, Does.Contain("\"sourceFindingIds\""));
        });
    }

    [Test]
    public void EnrichmentProjectionUsesDeterministicOrderedProvenanceWithoutChangingCandidates()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");
        HistoryIngestionResult original = HistoryIngestionFixture.Succeed(repository, first, second);
        var enrichment = new HistoryEnrichmentProjection(
            HistoryEnrichmentStatus.Unavailable,
            "no trusted source state",
            [new HistoryEnrichmentProvenance("zeta", "second"), new HistoryEnrichmentProvenance("alpha", "first")]);
        var projected = new HistoryIngestionResult(
            original.ObjectFormatName,
            original.AuthoredFrom,
            original.AuthoredTo,
            original.ResolvedFrom,
            original.ResolvedTo,
            original.Commits,
            original.ExcludedMergeCount,
            original.RenameCandidates,
            original.RenameComponents,
            original.LogicalFiles,
            original.CoChangeGraph,
            original.BottleneckAnalysis,
            original.OcpAnalysis,
            original.Configuration,
            original.HotspotAnalysis,
            enrichment);

        string json = HistoryIngestionJsonWriter.Write(projected);
        int alpha = json.IndexOf("\"kind\": \"alpha\"", StringComparison.Ordinal);
        int zeta = json.IndexOf("\"kind\": \"zeta\"", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"status\": \"unavailable\""));
            Assert.That(json, Does.Contain("\"reason\": \"no trusted source state\""));
            Assert.That(alpha, Is.GreaterThanOrEqualTo(0));
            Assert.That(zeta, Is.GreaterThan(alpha));
            Assert.That(json, Does.Contain("\"candidates\""));
        });
    }
}
