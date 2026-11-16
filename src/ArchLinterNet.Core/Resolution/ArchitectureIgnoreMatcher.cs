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
        return IsIgnored(sourceType, forbiddenReference, ignoredViolations, usageTracker, liveIdentity, out _);
    }

    // Also reports whether any matching entry came from a loaded baseline document (as opposed to a
    // policy-authored structured waiver) -- callers that feed baseline-comparison candidates need
    // this to keep waiver debt and finding debt separate. See
    // ArchitectureIgnoredViolation.IsFromLoadedBaseline.
    public static bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker,
        ArchitectureViolationIdentity? liveIdentity,
        out bool matchedByLoadedBaseline)
    {
        // This compatibility entry point is retained for focused direct matcher tests. Contract
        // executions build this immutable index once and reuse it for every finding.
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(ignoredViolations);
        string? waiverFingerprint = index.CreateLiveWaiverFingerprint(liveIdentity);
        return index.IsIgnored(
            sourceType,
            forbiddenReference,
            usageTracker,
            liveIdentity,
            waiverFingerprint,
            out matchedByLoadedBaseline);
    }

    internal static bool MatchesPattern(string value, string pattern)
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

    internal static bool ContainsGlobSyntax(string pattern)
    {
        return pattern.Contains('*', StringComparison.Ordinal)
               || pattern.Contains('?', StringComparison.Ordinal);
    }

    internal static string Normalize(string value)
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
