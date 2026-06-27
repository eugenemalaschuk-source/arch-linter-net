using System.Text;
using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Discovery;

internal static class ProjectPathGlob
{
    public static bool IsMatch(string relativePath, string pattern)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        Regex regex = BuildRegex(pattern.Replace('\\', '/'));
        return regex.IsMatch(normalizedPath);
    }

    private static Regex BuildRegex(string pattern)
    {
        StringBuilder builder = new("^");

        for (int i = 0; i < pattern.Length; i++)
        {
            char current = pattern[i];

            if (current == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                builder.Append(".*");
                i++;
                if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                {
                    i++;
                }
            }
            else if (current == '*')
            {
                builder.Append("[^/]*");
            }
            else
            {
                builder.Append(Regex.Escape(current.ToString()));
            }
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase);
    }
}
