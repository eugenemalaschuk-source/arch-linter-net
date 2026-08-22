using System.Text;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryCommitMetadataTests
{
    [Test]
    public void AuthorIdentityUsesTheEmailLowercasedByAsciiOnly()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        string second = repository.Commit("second");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.CanonicalAuthor, Is.EqualTo("fixture@example.com"));
    }

    [Test]
    public void CommitterEpochIsExactAndTheTimezoneTokenDoesNotShiftIt()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 -0500", "root");
        string second = SyntheticCommit(repository, [first], "author A <a@example.com> 1700000900 +0900", "committer A <a@example.com> 1700000900 +0900", "second");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.Commit.Committer.EpochSecondText, Is.EqualTo("1700000900"));
        Assert.That(commit.Commit.Committer.TimezoneToken, Is.EqualTo("+0900"));
    }

    [Test]
    public void EpochSecondsOutsideTheHostCalendarRangeAreRetainedExactly()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        const string Huge = "99999999999999999999";
        string second = SyntheticCommit(repository, [first], $"author A <a@example.com> {Huge} +0000", $"committer A <a@example.com> {Huge} +0000", "far future");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.Commit.Committer.EpochSecondText, Is.EqualTo(Huge));
    }

    [Test]
    public void LeadingZeroesAndNegativeZeroCollapseToCanonicalZero()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> -0 +0000", "committer A <a@example.com> -0 +0000", "root");
        string second = SyntheticCommit(repository, [first], "author A <a@example.com> 0007 +0000", "committer A <a@example.com> 0007 +0000", "second");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.Commit.Committer.EpochSecondText, Is.EqualTo("7"));
    }

    [Test]
    public void EncodingHeadersAreRetainedAsOrderedHexProvenanceWithoutTranscoding()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        string second = SyntheticCommit(
            repository,
            [first],
            "author A <a@example.com> 1700000900 +0000",
            "committer A <a@example.com> 1700000900 +0000",
            "second",
            extraHeaders: "encoding ISO-8859-1\n");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.Commit.EncodingHeaderHex, Is.EqualTo(new[] { "49534f2d383835392d31" }));
    }

    [Test]
    public void ADuplicateAuthorHeaderFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        string second = SyntheticCommit(
            repository,
            [first],
            "author A <a@example.com> 1700000900 +0000",
            "committer A <a@example.com> 1700000900 +0000",
            "second",
            extraHeaders: "author B <b@example.com> 1700000900 +0000\n");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("commit_metadata_malformed"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(second));
    }

    [Test]
    public void ADuplicateCommitterHeaderFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        string second = SyntheticCommit(
            repository,
            [first],
            "author A <a@example.com> 1700000900 +0000",
            "committer A <a@example.com> 1700000900 +0000",
            "second",
            extraHeaders: "committer B <b@example.com> 1700000900 +0000\n");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("commit_metadata_malformed"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(second));
    }

    [Test]
    public void AMalformedAuthorHeaderFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        string second = SyntheticCommit(repository, [first], "author A a@example.com 1700000900 +0000", "committer A <a@example.com> 1700000900 +0000", "second");

        Assert.That(HistoryIngestionFixture.Fail(repository, first, second).KindText, Is.EqualTo("commit_metadata_malformed"));
    }

    [Test]
    public void NonUtf8AuthorBytesFailClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        byte[] payload = BuildCommitBytes(
            repository.Git("rev-parse", $"{first}^{{tree}}").Trim(),
            [first],
            "author A <"u8.ToArray().Concat(new byte[] { 0xFF, 0xFE }).Concat("@example.com> 1700000900 +0000"u8.ToArray()).ToArray(),
            Encoding.ASCII.GetBytes("committer A <a@example.com> 1700000900 +0000"),
            Encoding.ASCII.GetBytes("second"),
            null);
        string second = repository.WriteRawObject("commit", payload);

        Assert.That(HistoryIngestionFixture.Fail(repository, first, second).KindText, Is.EqualTo("author_encoding_invalid"));
    }

    [Test]
    public void NonUtf8CommitMessageBytesFailClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        byte[] payload = BuildCommitBytes(
            repository.Git("rev-parse", $"{first}^{{tree}}").Trim(),
            [first],
            Encoding.ASCII.GetBytes("author A <a@example.com> 1700000900 +0000"),
            Encoding.ASCII.GetBytes("committer A <a@example.com> 1700000900 +0000"),
            [0x66, 0x69, 0x78, 0x20, 0xC3, 0x28],
            null);
        string second = repository.WriteRawObject("commit", payload);

        Assert.That(HistoryIngestionFixture.Fail(repository, first, second).KindText, Is.EqualTo("message_encoding_invalid"));
    }

    // A multi-line `gpgsig` continuation must not be mistaken for a direct header.
    [Test]
    public void ContinuationLinesDoNotBecomeDirectHeaders()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SyntheticCommit(repository, [], "author A <a@example.com> 1700000000 +0000", "committer A <a@example.com> 1700000000 +0000", "root");
        string second = SyntheticCommit(
            repository,
            [first],
            "author A <a@example.com> 1700000900 +0000",
            "committer A <a@example.com> 1700000900 +0000",
            "second",
            extraHeaders: "gpgsig -----BEGIN-----\n author B <b@example.com> 1 +0000\n encoding UTF-7\n -----END-----\n");

        CommitEvidence commit = HistoryIngestionFixture.Succeed(repository, first, second).Commits.Single();

        Assert.That(commit.CanonicalAuthor, Is.EqualTo("a@example.com"));
        Assert.That(commit.Commit.EncodingHeaderHex, Is.Empty);
    }

    private static string SyntheticCommit(
        GitTestRepository repository,
        string[] parents,
        string author,
        string committer,
        string message,
        string? extraHeaders = null)
    {
        // The root commit needs a real tree, so the first synthetic commit borrows an empty one.
        string tree = parents.Length == 0
            ? repository.Git("hash-object", "-w", "-t", "tree", "--stdin", "/dev/null").Trim()
            : repository.Git("rev-parse", $"{parents[0]}^{{tree}}").Trim();
        byte[] payload = BuildCommitBytes(
            tree,
            parents,
            Encoding.UTF8.GetBytes(author),
            Encoding.UTF8.GetBytes(committer),
            Encoding.UTF8.GetBytes(message),
            extraHeaders);
        return repository.WriteRawObject("commit", payload);
    }

    private static byte[] BuildCommitBytes(
        string tree,
        string[] parents,
        byte[] author,
        byte[] committer,
        byte[] message,
        string? extraHeaders)
    {
        using MemoryStream buffer = new();
        void AppendAscii(string text) => buffer.Write(Encoding.ASCII.GetBytes(text));
        AppendAscii($"tree {tree}\n");
        foreach (string parent in parents)
        {
            AppendAscii($"parent {parent}\n");
        }

        AppendAscii("author ");
        buffer.Write(author);
        AppendAscii("\ncommitter ");
        buffer.Write(committer);
        AppendAscii("\n");
        if (extraHeaders is not null)
        {
            AppendAscii(extraHeaders);
        }

        AppendAscii("\n");
        buffer.Write(message);
        AppendAscii("\n");
        return buffer.ToArray();
    }
}
