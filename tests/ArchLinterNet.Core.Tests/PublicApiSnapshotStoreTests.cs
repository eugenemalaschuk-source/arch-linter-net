using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Exercises the Contracts-level snapshot store against a real temp repository layout, which is what
// makes the policy-boundary rule (policy directory, or its parent when the policy lives in
// `architecture/`) observable rather than asserted only in the abstract.
[TestFixture]
public sealed class PublicApiSnapshotStoreTests
{
    private string _repositoryRoot = null!;
    private string _policyPath = null!;
    private IPublicApiSnapshotStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-snapshot-store-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "architecture", "api"));
        _policyPath = Path.Combine(_repositoryRoot, "architecture", "dependencies.arch.yml");
        File.WriteAllText(_policyPath, "version: 1\nname: Test\n");
        _store = new PublicApiSnapshotStore(ArchitectureFileSystem.Real);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, true);
        }
    }

    private string WriteSnapshot(string relativePath, params string[] signatures)
    {
        string absolute = Path.Combine(_repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion,
            "surface",
            signatures.Select(signature => new PublicApiSnapshotEntry("Acme.Module", signature)).ToArray())));
        return absolute;
    }

    [Test]
    public void ResolvePath_PolicyInArchitectureFolder_ResolvesAgainstRepositoryRoot()
    {
        string resolved = _store.ResolvePath(_policyPath, "architecture/api/surface.txt");

        Assert.That(
            resolved,
            Is.EqualTo(Path.GetFullPath(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt"))));
    }

    [Test]
    public void ResolvePath_AbsolutePath_Throws()
    {
        string absolute = Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt");

        Assert.That(
            () => _store.ResolvePath(_policyPath, absolute),
            Throws.InvalidOperationException.With.Message.Contains("absolute public API snapshot path"));
    }

    [Test]
    public void ResolvePath_EscapingPath_Throws()
    {
        Assert.That(
            () => _store.ResolvePath(_policyPath, "../../outside.txt"),
            Throws.InvalidOperationException.With.Message.Contains("outside the policy boundary"));
    }

    [Test]
    public void Exists_ReportsPresenceOfTheResolvedFile()
    {
        WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");
        string resolved = _store.ResolvePath(_policyPath, "architecture/api/surface.txt");
        string absent = _store.ResolvePath(_policyPath, "architecture/api/absent.txt");

        Assert.Multiple(() =>
        {
            Assert.That(_store.Exists(resolved), Is.True);
            Assert.That(_store.Exists(absent), Is.False);
        });
    }

    [Test]
    public void Read_ParsesTheSnapshotDocument()
    {
        string resolved = WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");

        PublicApiSnapshotDocument document = _store.Read(resolved, "architecture/api/surface.txt");

        Assert.Multiple(() =>
        {
            Assert.That(document.ContractId, Is.EqualTo("surface"));
            Assert.That(document.Entries.Select(entry => entry.Signature), Is.EqualTo(new[] { "class Acme.Module.Thing" }));
        });
    }

    [Test]
    public void Read_InvalidSnapshot_ThrowsNamingTheAuthoredPath()
    {
        string absolute = Path.Combine(_repositoryRoot, "architecture", "api", "broken.txt");
        File.WriteAllText(absolute, "@format arch-linter-net/public-api-snapshot\n@version 7\n");

        Assert.That(
            () => _store.Read(absolute, "architecture/api/broken.txt"),
            Throws.InvalidOperationException.With.Message.Contains("architecture/api/broken.txt"));
    }

    [Test]
    public void ResolvePath_PolicyOutsideArchitectureFolder_UsesPolicyDirectoryAsBoundary()
    {
        string flatPolicy = Path.Combine(_repositoryRoot, "dependencies.arch.yml");
        File.WriteAllText(flatPolicy, "version: 1\nname: Test\n");

        string resolved = _store.ResolvePath(flatPolicy, "api/surface.txt");

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo(Path.GetFullPath(Path.Combine(_repositoryRoot, "api", "surface.txt"))));
            Assert.That(
                () => _store.ResolvePath(flatPolicy, "../outside.txt"),
                Throws.InvalidOperationException);
        });
    }
}
