using System.Text;
using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Discovery;

internal static class ProjectPathGlob
{
    // pattern below is built dynamically from an author-supplied glob (via Regex.Escape plus
    // substitution, not a compile-time literal), so it cannot use [GeneratedRegex]. An explicit
    // timeout is this rule's (S6444) mitigation for a pathological pattern causing catastrophic
    // backtracking.
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

    public static bool IsMatch(string relativePath, string pattern)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        Regex regex = BuildRegex(pattern.Replace('\\', '/'));
        return regex.IsMatch(normalizedPath);
    }

    private static Regex BuildRegex(string pattern)
    {
        StringBuilder builder = new("^");

        int i = 0;
        while (i < pattern.Length)
        {
            char current = pattern[i];

            if (current == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                builder.Append(".*");
                i += 2;
                if (i < pattern.Length && pattern[i] == '/')
                {
                    i++;
                }
            }
            else if (current == '*')
            {
                builder.Append("[^/]*");
                i++;
            }
            else
            {
                builder.Append(Regex.Escape(current.ToString()));
                i++;
            }
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase, _matchTimeout);
    }
}
