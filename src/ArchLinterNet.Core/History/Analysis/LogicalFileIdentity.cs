using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// Baseline same-path identity plus accepted-lineage unions. Every event whose canonical path string
// is exactly equal shares one identity across the whole analyzed range — deliberately including
// delete/re-add generations, which is the v1 pathname-reuse conflation the reports must disclose.
internal sealed class LogicalFileIdentity
{
    private readonly Dictionary<string, int> _groupOfPath = new(StringComparer.Ordinal);
    private readonly List<List<string>> _pathsOfGroup = [];
    private readonly Dictionary<int, string> _canonicalPathOverride = [];

    public void RegisterPath(string path)
    {
        if (_groupOfPath.ContainsKey(path))
        {
            return;
        }

        _groupOfPath[path] = _pathsOfGroup.Count;
        _pathsOfGroup.Add([path]);
    }

    public void UnionLineage(IReadOnlyList<RenameCandidate> sequence)
    {
        int target = GroupOf(sequence[0].SourcePath);
        foreach (RenameCandidate candidate in sequence)
        {
            target = Union(target, GroupOf(candidate.SourcePath));
            target = Union(target, GroupOf(candidate.DestinationPath));
        }

        _canonicalPathOverride[target] = sequence[^1].DestinationPath;
    }

    public int GroupOf(string path)
    {
        RegisterPath(path);
        return _groupOfPath[path];
    }

    public IReadOnlyList<int> Groups() => [.. Enumerable.Range(0, _pathsOfGroup.Count).Where(group => _pathsOfGroup[group].Count > 0)];

    public string CanonicalPathOf(int group)
        => _canonicalPathOverride.TryGetValue(group, out string? canonical) ? canonical : _pathsOfGroup[group][0];

    // Aliases order by first in-range occurrence and then by canonical scalar-value string order; the
    // canonical path itself is never repeated as an alias.
    public IReadOnlyList<string> AliasesOf(int group, IReadOnlyDictionary<string, int> firstOccurrenceOrder)
    {
        string canonical = CanonicalPathOf(group);
        return [.. _pathsOfGroup[group]
            .Where(path => !string.Equals(path, canonical, StringComparison.Ordinal))
            .OrderBy(path => firstOccurrenceOrder.TryGetValue(path, out int order) ? order : int.MaxValue)
            .ThenBy(static path => path, HistoryScalarValueComparer.Instance)];
    }

    private int Union(int left, int right)
    {
        if (left == right)
        {
            return left;
        }

        foreach (string path in _pathsOfGroup[right])
        {
            _groupOfPath[path] = left;
            _pathsOfGroup[left].Add(path);
        }

        _pathsOfGroup[right] = [];
        if (_canonicalPathOverride.Remove(right, out string? moved))
        {
            _canonicalPathOverride[left] = moved;
        }

        return left;
    }
}
