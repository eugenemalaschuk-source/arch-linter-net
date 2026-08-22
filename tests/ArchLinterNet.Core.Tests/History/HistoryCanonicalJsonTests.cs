using System.Globalization;
using System.Text;
using ArchLinterNet.Core.Contracts;
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
    [NonParallelizable]
    public void CanonicalBytesIgnorePresentationCulture()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("café.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("café.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            string turkish = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            string invariant = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

            Assert.That(turkish, Is.EqualTo(invariant));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public void EscapingFollowsTheCanonicalProfile()
    {
        string quoted = CanonicalJsonWriter.Quote("a\"b\\c/d\u0001e\nf\u00E9");

        Assert.That(quoted, Is.EqualTo("\"a\\\"b\\\\c/d\\u0001e\\nfé\""));
    }

    [Test]
    public void ValidNonBmpScalarsAreRetainedAndUnpairedSurrogatesAreRejected()
    {
        Assert.That(CanonicalJsonWriter.Quote("café 😀"), Is.EqualTo("\"café 😀\""));

        foreach (string invalid in new[] { "prefix\uD800", "prefix\uDC00" })
        {
            CanonicalJsonUnicodeException exception = Assert.Throws<CanonicalJsonUnicodeException>(() => CanonicalJsonWriter.Quote(invalid))!;
            Assert.That(exception.Message, Does.Contain("index 6"));
        }
    }

    [Test]
    public void NonAsciiGitEvidenceHasTheExpectedUtf8ReportBytes()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/café.cs", "one\n");
        string first = repository.Commit("first");
        repository.Write("src/café.cs", "one\ntwo\n");
        string second = repository.Commit("second #12");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(json);

        Assert.Multiple(() =>
        {
            Assert.That(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), Is.False);
            Assert.That(Contains(bytes, "src/café.cs"u8.ToArray()), Is.True);
            Assert.That(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes), Is.EqualTo(json));
        });
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
            [
                new HistoryEnrichmentProvenance("source", "😀"),
                new HistoryEnrichmentProvenance("source", "é"),
                new HistoryEnrichmentProvenance("source", "e\u0301"),
            ]);
        HistoryIngestionResult projected = WithEnrichment(original, enrichment);

        string json = HistoryIngestionJsonWriter.Write(projected);
        int decomposed = json.IndexOf("\"value\": \"e\u0301\"", StringComparison.Ordinal);
        int composed = json.IndexOf("\"value\": \"é\"", StringComparison.Ordinal);
        int nonBmp = json.IndexOf("\"value\": \"😀\"", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"status\": \"unavailable\""));
            Assert.That(json, Does.Contain("\"reason\": \"no trusted source state\""));
            Assert.That(decomposed, Is.GreaterThanOrEqualTo(0));
            Assert.That(composed, Is.GreaterThan(decomposed));
            Assert.That(nonBmp, Is.GreaterThan(composed));
            Assert.That(json, Does.Contain("\"candidates\""));
        });
    }

    [Test]
    public void EveryEnrichmentStatusHasAnExplicitCanonicalProjection()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");
        HistoryIngestionResult original = HistoryIngestionFixture.Succeed(repository, first, second);

        foreach ((HistoryEnrichmentStatus status, string text) in new[]
        {
            (HistoryEnrichmentStatus.NotRequested, "not_requested"),
            (HistoryEnrichmentStatus.NotApplicable, "not_applicable"),
            (HistoryEnrichmentStatus.Available, "available"),
            (HistoryEnrichmentStatus.Unavailable, "unavailable"),
        })
        {
            var enrichment = new HistoryEnrichmentProjection(
                status,
                status == HistoryEnrichmentStatus.Unavailable ? "no trusted source" : null,
                context: status == HistoryEnrichmentStatus.Available
                    ? [new HistoryEnrichmentContext("ticket", "123")]
                    : []);

            string json = HistoryIngestionJsonWriter.Write(WithEnrichment(original, enrichment));

            Assert.That(json, Does.Contain($"\"status\": \"{text}\""));
        }
    }

    [Test]
    public void InvalidEnrichmentUnicodeFailsBeforeAReportCanBeReturned()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #12");
        HistoryIngestionResult original = HistoryIngestionFixture.Succeed(repository, first, second);
        HistoryIngestionResult invalid = WithEnrichment(
            original,
            new HistoryEnrichmentProjection(
                HistoryEnrichmentStatus.Available,
                context: [new HistoryEnrichmentContext("provider", "broken\uD800")]));

        Assert.That(
            () => HistoryIngestionJsonWriter.Write(invalid),
            Throws.TypeOf<CanonicalJsonUnicodeException>());
    }

    [Test]
    public void CandidateRecordsRetainStableSourceIdsAndExactQualificationEvidence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Write("B.cs", "two\n");
        string second = repository.Commit("together #12");
        repository.Write("A.cs", "three\n");
        repository.Write("B.cs", "three\n");
        string last = repository.Commit("again #12");
        var configuration = new HistoryAnalysisConfiguration
        {
            Thresholds = new HistoryAnalysisThresholds { CoChangeSignificance = 0m },
        };

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, last, configuration));
        string candidates = json[json.IndexOf("\"candidates\"", StringComparison.Ordinal)..];

        Assert.Multiple(() =>
        {
            Assert.That(candidates, Does.Contain("\"kind\": \"hotspot\""));
            Assert.That(candidates, Does.Contain("\"kind\": \"co_change_cluster\""));
            Assert.That(candidates, Does.Contain("\"sourceFindingIds\""));
            Assert.That(candidates, Does.Contain("\"qualifyingEdges\""));
            Assert.That(candidates, Does.Contain("\"significanceThreshold\": 0.000000000"));
        });
    }

    [Test]
    public void Sha256ReportPreservesFullObjectIdsAndCanonicalTaskKeyOrdering()
    {
        using GitTestRepository repository = GitTestRepository.CreateWithObjectFormat("sha256");
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #2 then #1");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));
        int one = json.IndexOf("\"id\": 1", StringComparison.Ordinal);
        int two = json.IndexOf("\"id\": 2", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"objectFormat\": \"sha256\""));
            Assert.That(json, Does.Contain($"\"id\": \"{second}\""));
            Assert.That(second, Has.Length.EqualTo(64));
            Assert.That(one, Is.GreaterThanOrEqualTo(0));
            Assert.That(two, Is.GreaterThan(one));
        });
    }

    private static HistoryIngestionResult WithEnrichment(HistoryIngestionResult original, HistoryEnrichmentProjection enrichment)
        => new(
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

    private static bool Contains(byte[] value, byte[] subsequence) => value.AsSpan().IndexOf(subsequence) >= 0;
}
