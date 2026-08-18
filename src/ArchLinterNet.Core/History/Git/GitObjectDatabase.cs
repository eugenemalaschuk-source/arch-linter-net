using System.IO;
using System.IO.Compression;
using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Loose-object and packfile access. Everything canonical ingestion needs is read from these bytes,
// which is what keeps evidence independent of the host Git binary, its configuration, and its diff
// machinery.
internal sealed class GitObjectDatabase : IDisposable
{
    private readonly GitRepositoryLayout _layout;
    private readonly List<GitPackFile> _packs = [];
    private bool _packsLoaded;

    public GitObjectDatabase(GitRepositoryLayout layout) => _layout = layout;

    public GitRawObject Read(GitObjectId id)
        => TryRead(id) ?? throw HistoryFailures.Fail(
            HistoryDiagnosticKind.ObjectMissing,
            $"The required Git object '{id.Hex}' could not be read from the object database.",
            objectId: id.Hex);

    public GitRawObject ReadOfKind(GitObjectId id, GitObjectKind kind)
    {
        GitRawObject raw = Read(id);
        return raw.Kind == kind
            ? raw
            : throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"The Git object '{id.Hex}' is a {raw.Kind.ToString().ToLowerInvariant()} where a {kind.ToString().ToLowerInvariant()} was required.",
                objectId: id.Hex);
    }

    public GitRawObject? TryRead(GitObjectId id)
    {
        if (id.IsEmpty)
        {
            return null;
        }

        GitRawObject? loose = TryReadLoose(id);
        if (loose is not null)
        {
            return loose;
        }

        foreach (GitPackFile pack in Packs())
        {
            GitRawObject? packed = pack.TryRead(id);
            if (packed is not null)
            {
                return packed;
            }
        }

        return null;
    }

    public void Dispose()
    {
        foreach (GitPackFile pack in _packs)
        {
            pack.Dispose();
        }

        _packs.Clear();
    }

    private IReadOnlyList<GitPackFile> Packs()
    {
        if (_packsLoaded)
        {
            return _packs;
        }

        _packsLoaded = true;
        string packDirectory = Path.Combine(_layout.ObjectsDirectory, "pack");
        if (!Directory.Exists(packDirectory))
        {
            return _packs;
        }

        foreach (string indexPath in Directory.GetFiles(packDirectory, "*.idx").OrderBy(static path => path, StringComparer.Ordinal))
        {
            string packPath = Path.ChangeExtension(indexPath, ".pack");
            if (File.Exists(packPath))
            {
                _packs.Add(new GitPackFile(packPath, indexPath, _layout.DigestLength, TryReadLoose));
            }
        }

        return _packs;
    }

    private GitRawObject? TryReadLoose(GitObjectId id)
    {
        string hex = id.Hex;
        if (hex.Length < 3)
        {
            return null;
        }

        string path = Path.Combine(_layout.ObjectsDirectory, hex[..2], hex[2..]);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] inflated = InflateLoose(path);
        int terminator = Array.IndexOf(inflated, (byte)0);
        if (terminator <= 0)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"The loose Git object '{hex}' has no object header terminator.",
                objectId: hex);
        }

        string header = Encoding.ASCII.GetString(inflated, 0, terminator);
        int space = header.IndexOf(' ', StringComparison.Ordinal);
        if (space <= 0 || !TryParseKind(header[..space], out GitObjectKind kind))
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"The loose Git object '{hex}' declares an unsupported object header '{header}'.",
                objectId: hex);
        }

        return new GitRawObject(kind, inflated[(terminator + 1)..]);
    }

    private static byte[] InflateLoose(string path)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZLibStream stream = new(file, CompressionMode.Decompress);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static bool TryParseKind(string text, out GitObjectKind kind)
    {
        switch (text)
        {
            case "commit":
                kind = GitObjectKind.Commit;
                return true;
            case "tree":
                kind = GitObjectKind.Tree;
                return true;
            case "blob":
                kind = GitObjectKind.Blob;
                return true;
            case "tag":
                kind = GitObjectKind.Tag;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
