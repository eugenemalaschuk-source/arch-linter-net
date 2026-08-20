namespace ArchLinterNet.Core.History.Configuration;

// This intentionally small path-pattern grammar is evaluated against canonical Git paths, which
// already use `/` and have passed strict UTF-8 validation. It therefore never needs to rewrite a
// separator, normalize Unicode, or ask the host filesystem how to compare a path.
internal sealed class HistoryPathGlob
{
    private readonly string[] _segments;

    private HistoryPathGlob(string[] segments)
    {
        _segments = segments;
    }

    public static HistoryPathGlob Parse(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidOperationException("history_analysis path patterns must be non-empty.");
        }

        if (pattern.Contains('\\'))
        {
            throw new InvalidOperationException("history_analysis path patterns must use '/' and must not contain backslashes.");
        }

        string[] segments = pattern.Split('/');
        foreach (string segment in segments)
        {
            ValidateSegment(segment, pattern);
        }

        return new HistoryPathGlob(segments);
    }

    public bool IsMatch(string canonicalPath)
    {
        string[] pathSegments = canonicalPath.Split('/');
        return IsMatch(pathSegments, pathIndex: 0, patternIndex: 0);
    }

    private bool IsMatch(IReadOnlyList<string> pathSegments, int pathIndex, int patternIndex)
    {
        if (patternIndex == _segments.Length)
        {
            return pathIndex == pathSegments.Count;
        }

        string patternSegment = _segments[patternIndex];
        if (string.Equals(patternSegment, "**", StringComparison.Ordinal))
        {
            for (int candidateIndex = pathIndex; candidateIndex <= pathSegments.Count; candidateIndex++)
            {
                if (IsMatch(pathSegments, candidateIndex, patternIndex + 1))
                {
                    return true;
                }
            }

            return false;
        }

        if (pathIndex == pathSegments.Count)
        {
            return false;
        }

        return (string.Equals(patternSegment, "*", StringComparison.Ordinal)
                || string.Equals(patternSegment, pathSegments[pathIndex], StringComparison.Ordinal))
            && IsMatch(pathSegments, pathIndex + 1, patternIndex + 1);
    }

    private static void ValidateSegment(string segment, string pattern)
    {
        if (segment.Length == 0)
        {
            throw new InvalidOperationException($"history_analysis path pattern '{pattern}' must not contain empty segments.");
        }

        if (string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"history_analysis path pattern '{pattern}' must not contain dot segments.");
        }

        if ((segment.Contains('*') && segment is not "*" and not "**")
            || segment.Contains('?')
            || segment.Contains('[')
            || segment.Contains(']'))
        {
            throw new InvalidOperationException(
                $"history_analysis path pattern '{pattern}' supports only whole-segment '*' and '**' wildcards.");
        }
    }
}
