namespace ArchLinterNet.Core.History.Git;

// Reads and caches parsed commit objects. Reachability, ancestry checks, and evidence construction
// all walk the same commits, so parsing each raw object once keeps a full-history traversal
// affordable without introducing a second source of truth.
internal sealed class GitCommitReader(GitObjectDatabase objects, int digestLength)
{
    private const string HexDigits = "0123456789abcdef";

    private readonly Dictionary<GitObjectId, GitCommit> _cache = [];

    public GitCommit Read(GitObjectId id)
    {
        if (_cache.TryGetValue(id, out GitCommit? cached))
        {
            return cached;
        }

        GitCommit parsed = Parse(id, objects.ReadOfKind(id, GitObjectKind.Commit).Payload);
        _cache[id] = parsed;
        return parsed;
    }

    private GitCommit Parse(GitObjectId id, byte[] payload)
    {
        (IReadOnlyList<GitHeaderLine> headers, byte[] message) = GitHeaderReader.Split(payload);
        GitObjectId tree = default;
        List<GitObjectId> parents = [];
        List<string> encodings = [];
        GitHeaderLine? author = null;
        GitHeaderLine? committer = null;
        foreach (GitHeaderLine header in headers)
        {
            switch (header.Name)
            {
                case "tree":
                    tree = ParseObjectId(header, id, "tree");
                    break;
                case "parent":
                    parents.Add(ParseObjectId(header, id, "parent"));
                    break;
                case "author":
                    author = RequireUnique(author, header, id, "author");
                    break;
                case "committer":
                    committer = RequireUnique(committer, header, id, "committer");
                    break;
                case "encoding":
                    encodings.Add(ToHex(header.Value));
                    break;
                default:
                    break;
            }
        }

        if (author is null || committer is null || tree.IsEmpty)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.CommitMetadataMalformed,
                $"Commit '{id.Hex}' is missing a required tree, author, or committer header.",
                objectId: id.Hex);
        }

        return new GitCommit(
            id,
            tree,
            parents,
            GitIdentityHeader.Parse("author", author.Value, id.Hex),
            GitIdentityHeader.Parse("committer", committer.Value, id.Hex),
            encodings,
            message);
    }

    private GitObjectId ParseObjectId(GitHeaderLine header, GitObjectId commitId, string headerName)
        => GitObjectId.TryParseHex(header.ValueText, digestLength, out GitObjectId parsed)
            ? parsed
            : throw HistoryFailures.Fail(
                HistoryDiagnosticKind.CommitMetadataMalformed,
                $"The '{headerName}' header of commit '{commitId.Hex}' is not a canonical object ID.",
                objectId: commitId.Hex);

    private static GitHeaderLine RequireUnique(GitHeaderLine? existing, GitHeaderLine header, GitObjectId commitId, string headerName)
        => existing is null
            ? header
            : throw HistoryFailures.Fail(
                HistoryDiagnosticKind.CommitMetadataMalformed,
                $"Commit '{commitId.Hex}' declares more than one direct '{headerName}' header.",
                objectId: commitId.Hex);

    private static string ToHex(byte[] value)
    {
        char[] characters = new char[value.Length * 2];
        for (int index = 0; index < value.Length; index++)
        {
            characters[index * 2] = HexDigits[value[index] >> 4];
            characters[(index * 2) + 1] = HexDigits[value[index] & 0x0F];
        }

        return new string(characters);
    }
}
