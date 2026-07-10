namespace ArchLinterNet.Core.Resolution;

internal sealed class NamespaceGlobPattern
{
    private readonly string _pattern;
    private readonly string[] _segments;
    private readonly int _wildcardCount;
    private readonly int _literalCount;

    private NamespaceGlobPattern(string pattern, string[] segments, int wildcardCount)
    {
        _pattern = pattern;
        _segments = segments;
        _wildcardCount = wildcardCount;
        _literalCount = segments.Length - wildcardCount;
    }

    public bool IsGlob => _wildcardCount > 0;

    public int WildcardCount => _wildcardCount;

    public int LiteralCount => _literalCount;

    public static NamespaceGlobPattern Parse(string pattern)
    {
        string[] segments = pattern.Split('.');
        ValidateSegments(pattern, segments);

        if (!ContainsGlobChars(pattern))
        {
            return new NamespaceGlobPattern(pattern, segments, 0);
        }

        int wildcardCount = CountWildcards(segments);

        if (segments.Length == 1 && segments[0] == "*")
        {
            throw new InvalidNamespacePatternException(pattern,
                "Bare wildcard '*' is not allowed. Must have at least one literal segment.");
        }

        if (segments[0] == "*")
        {
            throw new InvalidNamespacePatternException(pattern,
                "Leading wildcard is not allowed. '*' must have at least one literal segment before it.");
        }

        return new NamespaceGlobPattern(pattern, segments, wildcardCount);
    }

    public ArchitectureNamespaceMatch Match(string namespaceName)
    {
        string[] nsSegments = namespaceName.Split('.');

        if (nsSegments.Length < _segments.Length)
        {
            return new ArchitectureNamespaceMatch(false, _pattern, null);
        }

        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] != "*" && _segments[i] != nsSegments[i])
            {
                return new ArchitectureNamespaceMatch(false, _pattern, null);
            }
        }

        string resolvedPrefix = string.Join(".", nsSegments, 0, _segments.Length);
        return new ArchitectureNamespaceMatch(true, _pattern, resolvedPrefix);
    }

    public int SpecificityScore
    {
        get
        {
            int score = 0;
            score += _literalCount * 10;
            score -= _wildcardCount;
            return score;
        }
    }

    private static void ValidateSegments(string pattern, string[] segments)
    {
        foreach (string seg in segments)
        {
            if (seg.Length == 0)
            {
                throw new InvalidNamespacePatternException(pattern,
                    "Empty segment found (e.g. 'A..B', leading dot, or trailing dot).");
            }

            if (seg.Contains("**"))
            {
                throw new InvalidNamespacePatternException(pattern,
                    "Recursive wildcard '**' is not supported in this version.");
            }

            if (seg.Contains('?'))
            {
                throw new InvalidNamespacePatternException(pattern,
                    "Single-character wildcard '?' is not supported in this version.");
            }

            if (seg.Contains('[') || seg.Contains(']'))
            {
                throw new InvalidNamespacePatternException(pattern,
                    "Character classes '[..]' are not supported.");
            }

            if (seg.Contains('*') && seg != "*")
            {
                throw new InvalidNamespacePatternException(pattern,
                    $"Partial segment wildcard '{seg}' is not allowed. '*' must be a complete segment.");
            }
        }
    }

    private static int CountWildcards(string[] segments)
    {
        return segments.Count(seg => seg == "*");
    }

    private static bool ContainsGlobChars(string pattern)
    {
        return pattern.Contains('*');
    }
}
