namespace ArchLinterNet.Core.Resolution;

public sealed class ArchitectureIgnoreUsageTracker
{
    private readonly HashSet<int> _matchedIndexes = new();

    public void MarkMatched(int index)
    {
        _matchedIndexes.Add(index);
    }

    public bool IsMatched(int index)
    {
        return _matchedIndexes.Contains(index);
    }

    public IReadOnlySet<int> MatchedIndexes => _matchedIndexes;
}
