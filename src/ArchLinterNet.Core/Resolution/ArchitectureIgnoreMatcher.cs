using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureIgnoreMatcher
{
    // regexPattern below is built dynamically from an author-supplied glob (via Regex.Escape plus
    // substitution, not a compile-time literal), so it cannot use [GeneratedRegex]. An explicit
    // timeout on the static IsMatch call is this rule's (S6444) mitigation for a pathological
    // pattern causing catastrophic backtracking.
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

    public static bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker = null)
    {
        if (usageTracker == null)
        {
            for (int i = 0; i < ignoredViolations.Count; i++)
            {
                var ignore = ignoredViolations[i];
                if (MatchesPattern(sourceType, ignore.SourceType)
                    && MatchesPattern(forbiddenReference, ignore.ForbiddenReference))
                {
                    return true;
                }
            }

            return false;
        }

        bool anyMatched = false;

        for (int i = 0; i < ignoredViolations.Count; i++)
        {
            var ignore = ignoredViolations[i];
            if (MatchesPattern(sourceType, ignore.SourceType)
                && MatchesPattern(forbiddenReference, ignore.ForbiddenReference))
            {
                usageTracker.MarkMatched(i);
                anyMatched = true;
            }
        }

        return anyMatched;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        string normalizedValue = Normalize(value);
        string normalizedPattern = Normalize(pattern);

        if (normalizedPattern == "*")
        {
            return true;
        }

        if (normalizedPattern.EndsWith('*')
            && !normalizedPattern.Contains("**", StringComparison.Ordinal)
            && normalizedPattern.Count(ch => ch == '*') == 1
            && !normalizedPattern.Contains('?'))
        {
            return normalizedValue.StartsWith(normalizedPattern[..^1], StringComparison.Ordinal);
        }

        if (!ContainsGlobSyntax(normalizedPattern))
        {
            return normalizedValue == normalizedPattern;
        }

        string regexPattern = "^" + GlobToRegex(normalizedPattern) + "$";
        return Regex.IsMatch(normalizedValue, regexPattern, RegexOptions.CultureInvariant, _matchTimeout);
    }

    private static bool ContainsGlobSyntax(string pattern)
    {
        return pattern.Contains('*', StringComparison.Ordinal)
               || pattern.Contains('?', StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return value.Replace('\\', '/');
    }

    private static string GlobToRegex(string pattern)
    {
        string escaped = Regex.Escape(pattern);

        escaped = escaped.Replace(@"\*\*", "<<<DOUBLESTAR>>>");
        escaped = escaped.Replace(@"\*", @"[^/]*");
        escaped = escaped.Replace(@"\?", @"[^/]");
        escaped = escaped.Replace("<<<DOUBLESTAR>>>", ".*");

        return escaped;
    }
}
