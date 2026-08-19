using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Tree object parsing plus a cache. Consecutive commits share almost all of their subtrees, so
// caching parsed trees is what makes the subtree-skipping delta cheap in practice.
internal sealed class GitTreeReader(GitObjectDatabase objects, int digestLength)
{
    private readonly Dictionary<GitObjectId, IReadOnlyList<GitTreeEntry>> _cache = [];

    public IReadOnlyList<GitTreeEntry> Read(GitObjectId treeId)
    {
        if (treeId.IsEmpty)
        {
            return [];
        }

        if (_cache.TryGetValue(treeId, out IReadOnlyList<GitTreeEntry>? cached))
        {
            return cached;
        }

        IReadOnlyList<GitTreeEntry> entries = Parse(treeId, objects.ReadOfKind(treeId, GitObjectKind.Tree).Payload);
        _cache[treeId] = entries;
        return entries;
    }

    private IReadOnlyList<GitTreeEntry> Parse(GitObjectId treeId, byte[] payload)
    {
        List<GitTreeEntry> entries = [];
        int position = 0;
        while (position < payload.Length)
        {
            int space = Array.IndexOf(payload, (byte)' ', position);
            if (space < 0)
            {
                throw Malformed(treeId);
            }

            int terminator = Array.IndexOf(payload, (byte)0, space + 1);
            if (terminator < 0 || terminator + 1 + digestLength > payload.Length)
            {
                throw Malformed(treeId);
            }

            string mode = Encoding.ASCII.GetString(payload, position, space - position);
            byte[] name = payload[(space + 1)..terminator];
            GitObjectId id = GitObjectId.FromBytes(payload.AsSpan(terminator + 1, digestLength));
            entries.Add(new GitTreeEntry(mode, name, id));
            position = terminator + 1 + digestLength;
        }

        return entries;
    }

    private static HistoryFailureException Malformed(GitObjectId treeId)
        => HistoryFailures.Fail(
            HistoryDiagnosticKind.ObjectMalformed,
            $"The tree object '{treeId.Hex}' is not a well-formed tree.",
            objectId: treeId.Hex);
}
