using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureIgnoreMatcher
{
    public static bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        IEnumerable<ArchitectureIgnoredViolation> ignoredViolations)
    {
        return ignoredViolations.Any(ignore => MatchesPattern(sourceType, ignore.SourceType)
                                               && MatchesPattern(forbiddenReference, ignore.ForbiddenReference));
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

        if (normalizedPattern.EndsWith("*", StringComparison.Ordinal)
            && !normalizedPattern.Contains("**", StringComparison.Ordinal)
            && normalizedPattern.Count(ch => ch == '*') == 1
            && !normalizedPattern.Contains('?', StringComparison.Ordinal))
        {
            return normalizedValue.StartsWith(normalizedPattern[..^1], StringComparison.Ordinal);
        }

        if (!ContainsGlobSyntax(normalizedPattern))
        {
            return normalizedValue == normalizedPattern;
        }

        string regexPattern = "^" + GlobToRegex(normalizedPattern) + "$";
        return Regex.IsMatch(normalizedValue, regexPattern, RegexOptions.CultureInvariant);
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
