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
        List<YamlLine> lines = SplitLines(yaml);

        int headerLineCount = 0;
        while (headerLineCount < lines.Count && IsCommentOrBlank(lines[headerLineCount].Content))
        {
            headerLineCount++;
        }

        // A file that is nothing but comments and blank lines has no content to anchor against, so
        // every line is header.
        var unanchorable = new List<int>();
        int blockScalarIndent = -1;
        for (int index = headerLineCount; index < lines.Count; index++)
        {
            string line = lines[index].Content;
            if (IsBlockScalarContent(line, blockScalarIndent))
            {
                continue;
            }

            blockScalarIndent = -1;
            if (FindCommentColumn(line) >= 0)
            {
                unanchorable.Add(index + 1);
            }

            if (DeclaresBlockScalar(line))
            {
                blockScalarIndent = BlockScalarIndentation(line);
            }
        }

        // The leading block belongs to the reviewer. Preserve it byte-for-byte, including its line
        // endings and separator blanks; a blank-only prefix is not a comment header.
        bool hasLeadingComment = lines.Take(headerLineCount).Any(line => IsComment(line.Content));
        string header = !hasLeadingComment
            ? string.Empty
            : yaml[..lines[headerLineCount - 1].End];

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
        QuoteState quotes = default;

        for (int index = 0; index < line.Length; index++)
        {
            if (quotes.Consume(line, index))
            {
                continue;
            }

            if (line[index] == '#' && !quotes.InsideScalar && OpensCommentToken(line, index))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// A <c>#</c> opens a comment only at the start of a line or after whitespace; mid-token
    /// (<c>Tagged#1</c>) it is an ordinary character.
    /// </summary>
    private static bool OpensCommentToken(string line, int index)
    {
        return index == 0 || char.IsWhiteSpace(line[index - 1]);
    }

    private static bool IsBlockScalarContent(string line, int blockScalarIndent)
    {
        return blockScalarIndent >= 0 && (line.AsSpan().IsWhiteSpace() || IndentationOf(line) > blockScalarIndent);
    }

    private static bool DeclaresBlockScalar(string line)
    {
        int comment = FindCommentColumn(line);
        ReadOnlySpan<char> content = (comment < 0 ? line : line[..comment]).AsSpan().TrimEnd();
        int separator = content.IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        ReadOnlySpan<char> indicator = content[(separator + 1)..].TrimStart();
        if (indicator.IsEmpty || indicator[0] is not ('|' or '>'))
        {
            return false;
        }

        for (int index = 1; index < indicator.Length; index++)
        {
            if (indicator[index] is not ('+' or '-' or >= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static int IndentationOf(string line)
    {
        int indentation = 0;
        while (indentation < line.Length && char.IsWhiteSpace(line[indentation]))
        {
            indentation++;
        }

        return indentation;
    }

    private static int BlockScalarIndentation(string line)
    {
        int indentation = IndentationOf(line);
        return line.AsSpan(indentation).StartsWith("- ", StringComparison.Ordinal)
            ? indentation + 2
            : indentation;
    }

    /// <summary>
    /// Tracks whether the scan is inside a single- or double-quoted scalar, keeping that bookkeeping
    /// out of the comment search itself.
    /// </summary>
    private struct QuoteState
    {
        private bool _single;
        private bool _double;

        public bool InsideScalar => _single || _double;

        /// <summary>
        /// Consumes a quote character, updating state. Returns true when the character was a quote and
        /// the caller should move on.
        /// </summary>
        public bool Consume(string line, int index)
        {
            char current = line[index];

            if (current == '\'' && !_double)
            {
                _single = !_single;
                return true;
            }

            if (current != '"' || _single)
            {
                return false;
            }

            // A quote is escaped only when preceded by an odd run of backslashes. In particular,
            // two backslashes encode one literal slash and the following quote closes the scalar.
            if (!IsEscaped(line, index))
            {
                _double = !_double;
            }

            return true;
        }

        private static bool IsEscaped(string line, int index)
        {
            int backslashCount = 0;
            for (int cursor = index - 1; cursor >= 0 && line[cursor] == '\\'; cursor--)
            {
                backslashCount++;
            }

            return backslashCount % 2 != 0;
        }
    }

    private static List<YamlLine> SplitLines(string yaml)
    {
        var lines = new List<YamlLine>();
        int start = 0;
        while (start < yaml.Length)
        {
            int newline = yaml.IndexOf('\n', start);
            int end = newline < 0 ? yaml.Length : newline + 1;
            int contentEnd = newline < 0 ? end : newline;
            if (contentEnd > start && yaml[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            lines.Add(new YamlLine(yaml[start..contentEnd], end));
            start = end;
        }

        return lines;
    }

    private readonly record struct YamlLine(string Content, int End);

    private static bool IsComment(string line)
    {
        return line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);
    }

    private static bool IsCommentOrBlank(string line)
    {
        return line.AsSpan().IsWhiteSpace() || IsComment(line);
    }
}
