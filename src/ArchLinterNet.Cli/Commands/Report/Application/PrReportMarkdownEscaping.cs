using System.Text;

namespace ArchLinterNet.Cli.Commands.Report.Application;

internal static class PrReportMarkdownEscaping
{
    internal static string EscapeInlineCode(string value)
    {
        string normalized = Normalize(value);
        return normalized
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("`", "&#96;", StringComparison.Ordinal);
    }

    internal static string EscapeMarkdownText(string value)
    {
        string normalized = Normalize(value);
        var builder = new StringBuilder(normalized.Length);
        foreach (char character in normalized)
        {
            switch (character)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '\\':
                case '`':
                case '*':
                case '_':
                case '{':
                case '}':
                case '[':
                case ']':
                case '(':
                case ')':
                case '!':
                case '|':
                    builder.Append('\\').Append(character);
                    break;
                case '@':
                    builder.Append("&#64;");
                    break;
                case '#':
                    builder.Append("&#35;");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return NeutralizeBareAutolinks(builder.ToString());
    }

    private static string NeutralizeBareAutolinks(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            if (value.AsSpan(index).StartsWith("://", StringComparison.Ordinal))
            {
                builder.Append("\\://");
                index += 2;
                continue;
            }

            if (value.AsSpan(index).StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(value, index, 3);
                builder.Append("&#46;");
                index += 3;
                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    private static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }
}
