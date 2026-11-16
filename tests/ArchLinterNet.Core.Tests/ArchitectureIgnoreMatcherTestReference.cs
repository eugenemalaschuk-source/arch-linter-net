using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Tests;

internal static class ArchitectureIgnoreMatcherTestReference
{
    internal static bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker,
        ArchitectureViolationIdentity? liveIdentity,
        out bool matchedByLoadedBaseline)
    {
        matchedByLoadedBaseline = false;
        bool anyMatched = false;
        for (int index = 0; index < ignoredViolations.Count; index++)
        {
            ArchitectureIgnoredViolation ignore = ignoredViolations[index];
            if (ignore.WaiverValidationError is not null)
            {
                continue;
            }

            bool matches;
            if (ignore.HasStructuredWaiverFields)
            {
                matches = liveIdentity is not null
                    && ignore.Target is not null
                    && string.Equals(
                        ignore.Target.Fingerprint,
                        ArchitectureWaiverTargetFingerprint.Create(liveIdentity),
                        StringComparison.Ordinal);
            }
            else if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion && liveIdentity is not null)
            {
                matches = string.Equals(ignore.ContractFamily, liveIdentity.ContractFamily, StringComparison.Ordinal)
                    && string.Equals(ignore.Kind, liveIdentity.Kind, StringComparison.Ordinal)
                    && string.Equals(ignore.SourceAssembly, liveIdentity.SourceAssembly, StringComparison.Ordinal)
                    && string.Equals(ignore.SourceType, liveIdentity.SourceType, StringComparison.Ordinal)
                    && string.Equals(ignore.SourceMember, liveIdentity.SourceMember, StringComparison.Ordinal)
                    && string.Equals(ignore.TargetAssembly, liveIdentity.TargetAssembly, StringComparison.Ordinal)
                    && string.Equals(ignore.TargetType, liveIdentity.TargetType, StringComparison.Ordinal)
                    && string.Equals(ignore.TargetMember, liveIdentity.TargetMember, StringComparison.Ordinal)
                    && (ignore.Occurrence ?? 0) == liveIdentity.Occurrence
                    && string.Equals(ignore.Configuration, liveIdentity.Configuration, StringComparison.Ordinal);
            }
            else
            {
                matches = ReferenceMatchesPattern(sourceType, ignore.SourceType)
                    && ReferenceMatchesPattern(forbiddenReference, ignore.ForbiddenReference);
            }

            if (!matches)
            {
                continue;
            }

            if (usageTracker is null)
            {
                matchedByLoadedBaseline = ignore.IsFromLoadedBaseline;
                return true;
            }

            usageTracker.MarkMatched(index);
            anyMatched = true;
            matchedByLoadedBaseline |= ignore.IsFromLoadedBaseline;
        }

        return anyMatched;
    }

    private static bool ReferenceMatchesPattern(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        string normalizedValue = value.Replace('\\', '/');
        string normalizedPattern = pattern.Replace('\\', '/');
        if (normalizedPattern == "*")
        {
            return true;
        }

        if (normalizedPattern.EndsWith('*')
            && !normalizedPattern.Contains("**", StringComparison.Ordinal)
            && normalizedPattern.Count(character => character == '*') == 1
            && !normalizedPattern.Contains('?'))
        {
            return normalizedValue.StartsWith(normalizedPattern[..^1], StringComparison.Ordinal);
        }

        if (!normalizedPattern.Contains('*', StringComparison.Ordinal)
            && !normalizedPattern.Contains('?', StringComparison.Ordinal))
        {
            return normalizedValue == normalizedPattern;
        }

        string escaped = Regex.Escape(normalizedPattern)
            .Replace(@"\*\*", "<<<DOUBLESTAR>>>")
            .Replace(@"\*", @"[^/]*")
            .Replace(@"\?", @"[^/]")
            .Replace("<<<DOUBLESTAR>>>", ".*");
        return Regex.IsMatch(normalizedValue, "^" + escaped + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

}
