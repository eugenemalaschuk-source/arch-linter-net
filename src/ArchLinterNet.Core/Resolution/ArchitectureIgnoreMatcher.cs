using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

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
        ArchitectureIgnoreUsageTracker? usageTracker = null,
        ArchitectureViolationIdentity? liveIdentity = null)
    {
        if (usageTracker == null)
        {
            for (int i = 0; i < ignoredViolations.Count; i++)
            {
                if (Matches(sourceType, forbiddenReference, liveIdentity, ignoredViolations[i]))
                {
                    return true;
                }
            }

            return false;
        }

        bool anyMatched = false;

        for (int i = 0; i < ignoredViolations.Count; i++)
        {
            if (Matches(sourceType, forbiddenReference, liveIdentity, ignoredViolations[i]))
            {
                usageTracker.MarkMatched(i);
                anyMatched = true;
            }
        }

        return anyMatched;
    }

    // A `version: 2`-sourced ignore entry (IdentityVersion == 2) was merged from a structured
    // baseline and is matched by exact identity equality — including assembly, member, and
    // occurrence — never by glob text matching. Manually authored ignores and entries merged from
    // a `version: 1` baseline have no IdentityVersion and keep the legacy glob-pair behavior
    // exactly as before.
    private static bool Matches(
        string sourceType, string forbiddenReference, ArchitectureViolationIdentity? liveIdentity, ArchitectureIgnoredViolation ignore)
    {
        if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion && liveIdentity != null)
        {
            return MatchesIdentity(liveIdentity, ignore);
        }

        return MatchesPattern(sourceType, ignore.SourceType) && MatchesPattern(forbiddenReference, ignore.ForbiddenReference);
    }

    private static bool MatchesIdentity(ArchitectureViolationIdentity live, ArchitectureIgnoredViolation ignore)
    {
        return string.Equals(ignore.ContractFamily, live.ContractFamily, StringComparison.Ordinal)
            && string.Equals(ignore.Kind, live.Kind, StringComparison.Ordinal)
            && string.Equals(ignore.SourceAssembly, live.SourceAssembly, StringComparison.Ordinal)
            && string.Equals(ignore.SourceType, live.SourceType, StringComparison.Ordinal)
            && string.Equals(ignore.SourceMember, live.SourceMember, StringComparison.Ordinal)
            && string.Equals(ignore.TargetAssembly, live.TargetAssembly, StringComparison.Ordinal)
            && string.Equals(ignore.TargetType, live.TargetType, StringComparison.Ordinal)
            && string.Equals(ignore.TargetMember, live.TargetMember, StringComparison.Ordinal)
            && (ignore.Occurrence ?? 0) == live.Occurrence
            && string.Equals(ignore.Configuration, live.Configuration, StringComparison.Ordinal);
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
