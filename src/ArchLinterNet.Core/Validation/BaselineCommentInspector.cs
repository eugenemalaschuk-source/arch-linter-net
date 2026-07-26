namespace ArchLinterNet.Core.Validation;

/// <summary>
/// What can and cannot be preserved when an existing baseline file is rewritten.
/// </summary>
/// <param name="Header">
/// The reviewed leading block of comment and blank lines, verbatim and including its trailing
/// newline, or an empty string when the file starts with content. Re-emitted above the regenerated
/// document.
/// </param>
/// <param name="UnanchorableCommentLines">
/// 1-based line numbers of comment lines at or after the first content line. These cannot be
/// re-anchored, because the serializer rebuilds the mapping from the model and no stable
/// input-line-to-output-line relationship survives adding, removing, or reordering entries.
/// </param>
public sealed record BaselineCommentInspection(
    string Header,
    IReadOnlyList<int> UnanchorableCommentLines)
{
    public bool CanRoundTrip => UnanchorableCommentLines.Count == 0;

    public bool HasHeader => Header.Length > 0;
}

/// <summary>
/// Splits a baseline file's comments into the part a rewrite can preserve (the leading header) and
/// the part it cannot (anything interleaved with content).
/// </summary>
/// <remarks>
/// This is deliberately a line-level inspection rather than a YAML round-trip: guessing where an
/// interior comment belongs in a regenerated document risks moving a reviewer's note onto the wrong
/// entry, which is worse than refusing.
/// <para>
/// A comment is any <c>#</c> that starts a comment token in YAML — whether it begins the line or
/// trails content on it (<c>reason: legacy debt # reviewed by Alice</c>). Both forms are reviewed
/// content the serializer would drop, so both block an in-place rewrite. <c>#</c> inside a quoted
/// scalar is not a comment and is correctly ignored; a <c>#</c> in an unquoted scalar with no
/// preceding space is likewise not a comment token.
/// </para>
/// </remarks>
public static class BaselineCommentInspector
{
    public static BaselineCommentInspection Inspect(string yaml)
    {
        string[] lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        int headerLineCount = 0;
        while (headerLineCount < lines.Length && IsCommentOrBlank(lines[headerLineCount]))
        {
            headerLineCount++;
        }

        // A file that is nothing but comments and blank lines has no content to anchor against, so
        // every line is header.
        var unanchorable = new List<int>();
        for (int index = headerLineCount; index < lines.Length; index++)
        {
            if (FindCommentColumn(lines[index]) >= 0)
            {
                unanchorable.Add(index + 1);
            }
        }

        // Trailing blank lines of the leading block are separator, not content worth re-emitting, and
        // a leading block with no comment in it at all is just whitespace — neither is a header.
        int headerEnd = headerLineCount;
        while (headerEnd > 0 && !IsComment(lines[headerEnd - 1]))
        {
            headerEnd--;
        }

        string header = headerEnd == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines.Take(headerEnd)) + Environment.NewLine;

        return new BaselineCommentInspection(header, unanchorable);
    }

    /// <summary>
    /// The actionable form of an unpreservable-comment refusal: which lines block the rewrite, and
    /// how to get the proposed content in order to merge them by hand.
    /// </summary>
    public static string DescribeRefusal(string command, string baselinePath, IReadOnlyList<int> unanchorableCommentLines)
    {
        return $"Baseline '{baselinePath}' has comments that cannot be safely preserved by {command}: " +
            $"line(s) {string.Join(", ", unanchorableCommentLines)}. " +
            "Only a leading comment header is preserved, because a rewritten document has no stable " +
            "position for a comment that sits next to an entry. " +
            $"Re-run with --dry-run to print the proposed document, merge those comments into it by hand, " +
            "or move them into the file's leading header block and re-run.";
    }

    /// <summary>
    /// Column of the first comment token on the line, or -1 when there is none. Tracks quoting so a
    /// <c>#</c> inside a scalar is not mistaken for a comment, and requires a leading space for a
    /// trailing comment, as YAML does.
    /// </summary>
    private static int FindCommentColumn(string line)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];

            if (current == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (current == '"' && !inSingleQuote)
            {
                // A backslash-escaped quote inside a double-quoted scalar does not close it.
                bool escaped = index > 0 && line[index - 1] == '\\';
                if (!escaped)
                {
                    inDoubleQuote = !inDoubleQuote;
                }

                continue;
            }

            if (current != '#' || inSingleQuote || inDoubleQuote)
            {
                continue;
            }

            // '#' opens a comment at the start of a line's content, or when preceded by whitespace.
            // Mid-token (`a#b`) it is an ordinary character.
            if (index == 0 || char.IsWhiteSpace(line[index - 1]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsComment(string line)
    {
        return line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);
    }

    private static bool IsCommentOrBlank(string line)
    {
        return line.AsSpan().IsWhiteSpace() || IsComment(line);
    }
}
