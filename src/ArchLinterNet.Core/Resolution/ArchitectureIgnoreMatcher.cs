using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureIgnoreMatcher
{
    // regexPattern below is built dynamically from an author-supplied glob (via Regex.Escape plus
    // substitution, not a compile-time literal), so it cannot use [GeneratedRegex]. An explicit
    // timeout on the static IsMatch call is this rule's (S6444) mitigation for a pathological
    // pattern causing catastrophic backtracking.
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

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

    internal static CompiledPattern CompilePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return CompiledPattern.Never;
        }

        string normalizedPattern = Normalize(pattern);
        if (normalizedPattern == "*")
        {
            return CompiledPattern.Any;
        }

        if (normalizedPattern.EndsWith('*')
            && !normalizedPattern.Contains("**", StringComparison.Ordinal)
            && normalizedPattern.Count(ch => ch == '*') == 1
            && !normalizedPattern.Contains('?'))
        {
            return CompiledPattern.Prefix(normalizedPattern[..^1]);
        }

        if (!ContainsGlobSyntax(normalizedPattern))
        {
            return CompiledPattern.Exact(normalizedPattern);
        }

        string regexPattern = "^" + GlobToRegex(normalizedPattern) + "$";
        return CompiledPattern.Regex(new Regex(regexPattern, RegexOptions.CultureInvariant, _matchTimeout));
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

    internal sealed class CompiledPattern
    {
        private readonly MatchKind _kind;
        private readonly string? _pattern;
        private readonly Regex? _regex;

        private CompiledPattern(MatchKind kind, string? pattern = null, Regex? regex = null)
        {
            _kind = kind;
            _pattern = pattern;
            _regex = regex;
        }

        internal static CompiledPattern Never { get; } = new(MatchKind.Never);

        internal static CompiledPattern Any { get; } = new(MatchKind.Any);

        internal static CompiledPattern Exact(string pattern)
        {
            return new CompiledPattern(MatchKind.Exact, pattern);
        }

        internal static CompiledPattern Prefix(string pattern)
        {
            return new CompiledPattern(MatchKind.Prefix, pattern);
        }

        internal static CompiledPattern Regex(Regex regex)
        {
            return new CompiledPattern(MatchKind.Regex, regex: regex);
        }

        internal bool IsMatch(string value)
        {
            string normalizedValue = Normalize(value);
            return _kind switch
            {
                MatchKind.Never => false,
                MatchKind.Any => true,
                MatchKind.Exact => normalizedValue == _pattern,
                MatchKind.Prefix => normalizedValue.StartsWith(_pattern!, StringComparison.Ordinal),
                MatchKind.Regex => _regex!.IsMatch(normalizedValue),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private enum MatchKind
        {
            Never,
            Any,
            Exact,
            Prefix,
            Regex,
        }
    }
}
