using System.IO.Compression;
using System.Text;
using ArchLinterNet.Core.History;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

// A loose object or packfile is untrusted bytes on disk: corruption must surface as a stable
// HistoryDiagnostic, never as a raw runtime exception escaping the fail-closed boundary.
[TestFixture]
public sealed class HistoryCorruptObjectTests
{
    [Test]
    public void ALooseObjectWhoseDeclaredSizeDisagreesWithItsPayloadFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        // The loose-object grammar is `<type> SP <size> NUL<payload>`. A blob claiming size 999 but
        // carrying only 3 payload bytes is structurally invalid and must not be accepted as "abc".
        string blobId = WriteCorruptLooseObject(repository, "blob 999\0abc"u8.ToArray());
        string second = ReferenceCorruptBlob(repository, first, blobId, "a.txt");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("object_malformed"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(blobId));
    }

    [Test]
    public void ALooseObjectWithANonNumericSizeFieldFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        string blobId = WriteCorruptLooseObject(repository, "blob garbage\0abc"u8.ToArray());
        string second = ReferenceCorruptBlob(repository, first, blobId, "a.txt");

        Assert.That(HistoryIngestionFixture.Fail(repository, first, second).KindText, Is.EqualTo("object_malformed"));
    }

    [Test]
    public void ATruncatedZlibLooseObjectFailsClosedInsteadOfThrowing()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second");
        string blobId = repository.Git("rev-parse", $"{second}:a.txt").Trim();
        string path = Path.Combine(repository.Path, ".git", "objects", blobId[..2], blobId[2..]);
        byte[] original = File.ReadAllBytes(path);
        File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllBytes(path, original[..(original.Length / 2)]);

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("object_malformed"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(blobId));
    }

    [Test]
    public void ATruncatedPackfileFailsClosedInsteadOfThrowing()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", string.Concat(Enumerable.Range(0, 200).Select(static line => $"line {line}\n")));
        string first = repository.Commit("first");
        repository.Write("a.txt", string.Concat(Enumerable.Range(0, 200).Select(static line => $"line {line} changed\n")));
        string second = repository.Commit("second");
        repository.Git("repack", "-a", "-d", "-f", "-q");

        // A repacked-from-scratch pack this small contains only the objects this range needs, so
        // cutting it in half reliably lands inside some object's compressed stream rather than only
        // clipping the trailing 20-byte pack checksum.
        string packPath = Directory.GetFiles(Path.Combine(repository.Path, ".git", "objects", "pack"), "*.pack").Single();
        byte[] original = File.ReadAllBytes(packPath);
        File.SetAttributes(packPath, FileAttributes.Normal);
        File.WriteAllBytes(packPath, original[..(original.Length / 2)]);

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("object_malformed"));
    }

    [Test]
    public void ATruncatedPackIndexFailsClosedInsteadOfThrowing()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second");
        repository.Git("repack", "-a", "-d", "-f", "-q");

        string indexPath = Directory.GetFiles(Path.Combine(repository.Path, ".git", "objects", "pack"), "*.idx").Single();
        byte[] original = File.ReadAllBytes(indexPath);
        File.SetAttributes(indexPath, FileAttributes.Normal);
        File.WriteAllBytes(indexPath, original[..(original.Length / 2)]);

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("object_malformed"));
    }

    // Writes a structurally invalid loose object directly (bypassing git entirely, since even
    // `hash-object --literally` still writes a well-formed header/size pair).
    private static string WriteCorruptLooseObject(GitTestRepository repository, byte[] rawContent)
    {
        string id = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(rawContent));
        string directory = Path.Combine(repository.Path, ".git", "objects", id[..2]);
        Directory.CreateDirectory(directory);
        using FileStream file = File.Create(Path.Combine(directory, id[2..]));
        using ZLibStream zlib = new(file, CompressionLevel.Fastest);
        zlib.Write(rawContent);
        return id;
    }

    // Builds a new commit whose tree references the corrupt blob at `path`, without going through
    // `git commit` (which would itself refuse a tree entry pointing at an object it cannot verify).
    private static string ReferenceCorruptBlob(GitTestRepository repository, string parent, string blobId, string path)
    {
        string treeLine = $"100644 {path}\0";
        byte[] treeBytes = [.. Encoding.ASCII.GetBytes(treeLine), .. Convert.FromHexString(blobId)];
        string treeId = repository.WriteRawObject("tree", treeBytes);
        string payload = $"tree {treeId}\nparent {parent}\n"
            + "author A <a@example.com> 1700007200 +0000\n"
            + "committer A <a@example.com> 1700007200 +0000\n\nreference corrupt blob\n";
        return repository.WriteRawObject("commit", Encoding.ASCII.GetBytes(payload));
    }
}
