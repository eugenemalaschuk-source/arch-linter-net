using System.Text;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Git;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

// Path semantics are asserted against raw tree bytes rather than through the working tree, because
// macOS and Windows filesystems normalize or reject the very byte sequences under test.
[TestFixture]
public sealed class HistoryPathSemanticsTests
{
    // Precomposed U+00E9 versus decomposed e + U+0301: canonically equivalent, scalar-distinct.
    private const string Precomposed = "caf\u00E9.txt";
    private const string Decomposed = "cafe\u0301.txt";

    [Test]
    public void CanonicallyEquivalentButScalarDistinctPathsStayDistinct()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SeedRoot(repository);
        string blob = repository.WriteRawObject("blob", "content\n"u8.ToArray());
        string tree = WriteTree(repository, [(Precomposed, blob), (Decomposed, blob)]);
        string second = WriteCommit(repository, tree, first);

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        // The synthetic tree replaces the seed file, so its delete event is also canonical evidence.
        Assert.That(
            result.LogicalFiles.Select(static file => file.CanonicalPath),
            Is.EqualTo(new[] { Decomposed, Precomposed, "seed.txt" }));
    }

    [Test]
    public void NonUtf8GitPathsFailClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = SeedRoot(repository);
        string blob = repository.WriteRawObject("blob", "content\n"u8.ToArray());
        string tree = WriteRawTree(repository, [("100644", new byte[] { 0x61, 0xFF, 0x2E, 0x74, 0x78, 0x74 }, blob)]);
        string second = WriteCommit(repository, tree, first);

        Assert.That(HistoryIngestionFixture.Fail(repository, first, second).KindText, Is.EqualTo("path_encoding_invalid"));
    }

    [Test]
    public void ScalarValueOrderingPlacesSupplementaryScalarsAboveEveryBmpScalar()
    {
        // U+1F600 is above U+FF21, but its UTF-16 lead surrogate U+D83D is below it.
        Assert.That(HistoryScalarValueComparer.Compare("\U0001F600", "\uFF21"), Is.GreaterThan(0));
        Assert.That(string.CompareOrdinal("\U0001F600", "\uFF21"), Is.LessThan(0));
        Assert.That(HistoryScalarValueComparer.Compare("ab", "abc"), Is.LessThan(0));
        Assert.That(HistoryScalarValueComparer.Compare("abc", "abc"), Is.Zero);
    }

    private static string SeedRoot(GitTestRepository repository)
    {
        repository.Write("seed.txt", "seed\n");
        return repository.Commit("seed");
    }

    private static string WriteTree(GitTestRepository repository, IReadOnlyList<(string Name, string Blob)> entries)
        => WriteRawTree(repository, [.. entries.Select(entry => ("100644", Encoding.UTF8.GetBytes(entry.Name), entry.Blob))]);

    // Git orders tree entries by raw name bytes; the fixture sorts them so hash-object accepts the
    // object it is handed.
    private static string WriteRawTree(GitTestRepository repository, IReadOnlyList<(string Mode, byte[] Name, string Blob)> entries)
    {
        using MemoryStream buffer = new();
        foreach ((string mode, byte[] name, string blob) in entries.OrderBy(static entry => entry.Name, ByteSequenceComparer.Instance))
        {
            buffer.Write(Encoding.ASCII.GetBytes(mode + " "));
            buffer.Write(name);
            buffer.WriteByte(0);
            buffer.Write(Convert.FromHexString(blob));
        }

        return repository.WriteRawObject("tree", buffer.ToArray());
    }

    private static string WriteCommit(GitTestRepository repository, string tree, string parent)
    {
        string payload = $"tree {tree}\nparent {parent}\n"
            + "author A <a@example.com> 1700003600 +0000\n"
            + "committer A <a@example.com> 1700003600 +0000\n\npaths\n";
        return repository.WriteRawObject("commit", Encoding.ASCII.GetBytes(payload));
    }

    private sealed class ByteSequenceComparer : IComparer<byte[]>
    {
        public static ByteSequenceComparer Instance { get; } = new();

        public int Compare(byte[]? x, byte[]? y) => (x ?? []).AsSpan().SequenceCompareTo(y ?? []);
    }
}
