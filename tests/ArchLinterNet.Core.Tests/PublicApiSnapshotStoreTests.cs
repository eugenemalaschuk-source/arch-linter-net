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
    private static readonly string[] _value = { "class Acme.Module.Thing" };
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
            Assert.That(document.Entries.Select(entry => entry.Signature), Is.EqualTo(_value));
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
    public void IsSameFile_ExactPath_AlwaysMatches()
    {
        string resolved = WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");

        Assert.That(_store.IsSameFile(resolved, resolved), Is.True);
    }

    // Whether a differently-cased path names the same file depends on the actual host filesystem,
    // not an assumption about the OS — so the test derives its own ground truth (does this host
    // resolve the case-variant path to the file we wrote?) and asserts IsSameFile agrees with it,
    // rather than hardcoding an expected result that would be wrong on some CI runners.
    [Test]
    public void IsSameFile_CaseVariant_MatchesRealFilesystemBehavior()
    {
        string resolved = WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");
        string caseVariant = Path.Combine(Path.GetDirectoryName(resolved)!, "Surface.txt");
        bool hostIsCaseInsensitive = File.Exists(caseVariant);

        Assert.That(_store.IsSameFile(caseVariant, resolved), Is.EqualTo(hostIsCaseInsensitive));
    }

    // The regressed bug: two paths both existing does not prove they are the same file. Only
    // reproducible on a case-sensitive host, where "Surface.txt" and "surface.txt" can genuinely
    // coexist as separate directory entries.
    [Test]
    public void IsSameFile_TwoDistinctCaseVariantFiles_DoesNotMatch()
    {
        string lower = WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");
        string upper = Path.Combine(Path.GetDirectoryName(lower)!, "Surface.txt");

        if (File.Exists(upper))
        {
            Assert.Ignore("Host filesystem is case-insensitive; 'Surface.txt' and 'surface.txt' cannot coexist.");
        }

        File.WriteAllText(upper, PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion, "surface", Array.Empty<PublicApiSnapshotEntry>())));

        Assert.That(_store.IsSameFile(upper, lower), Is.False);
    }

    // The second regressed bug: comparing directory names with OrdinalIgnoreCase treats sibling
    // case-variant directories ("api" and "API") as the same directory, so a same-named file inside
    // each looks like a single match at the leaf level even though the two files are genuinely
    // distinct. Only reproducible on a case-sensitive host, where both directories can coexist.
    [Test]
    public void IsSameFile_SameLeafNameInSiblingCaseVariantDirectories_DoesNotMatch()
    {
        string lowerDirectory = Path.Combine(_repositoryRoot, "architecture", "api");
        string upperDirectory = Path.Combine(_repositoryRoot, "architecture", "API");

        if (Directory.Exists(upperDirectory))
        {
            Assert.Ignore("Host filesystem is case-insensitive; 'api' and 'API' cannot coexist as sibling directories.");
        }

        Directory.CreateDirectory(upperDirectory);
        string lower = WriteSnapshot("architecture/api/surface.txt", "class Acme.Module.Thing");
        string upper = Path.Combine(upperDirectory, "surface.txt");
        File.WriteAllText(upper, PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion, "surface", Array.Empty<PublicApiSnapshotEntry>())));

        Assert.That(_store.IsSameFile(upper, lower), Is.False);
    }

    [Test]
    public void IsSameFile_NeitherPathExists_DoesNotMatch()
    {
        string directory = Path.Combine(_repositoryRoot, "architecture", "api");
        string first = Path.Combine(directory, "Ghost.txt");
        string second = Path.Combine(directory, "ghost.txt");

        Assert.That(_store.IsSameFile(first, second), Is.False);
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
