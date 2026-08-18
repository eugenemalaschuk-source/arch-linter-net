namespace ArchLinterNet.Core.History.Tasks;

// Mandatory canonical provenance for one extractor match. The span is a half-open raw message byte
// range, not a decoded character range, so provenance stays anchored to the commit object bytes.
internal sealed class TaskKeyMatch(string extractorId, TaskKey key, int spanStart, int spanEnd, string matchedText)
    : IEquatable<TaskKeyMatch>
{
    public string ExtractorId { get; } = extractorId;

    public TaskKey Key { get; } = key;

    public int SpanStart { get; } = spanStart;

    public int SpanEnd { get; } = spanEnd;

    public string MatchedText { get; } = matchedText;

    public bool OverlapsWith(TaskKeyMatch other) => SpanStart < other.SpanEnd && other.SpanStart < SpanEnd;

    public bool Equals(TaskKeyMatch? other)
        => other is not null
            && string.Equals(ExtractorId, other.ExtractorId, StringComparison.Ordinal)
            && Key.Equals(other.Key)
            && SpanStart == other.SpanStart
            && SpanEnd == other.SpanEnd
            && string.Equals(MatchedText, other.MatchedText, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TaskKeyMatch);

    public override int GetHashCode() => HashCode.Combine(ExtractorId, Key, SpanStart, SpanEnd, MatchedText);
}
