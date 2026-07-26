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
/// entry, which is worse than refusing. A <c>#</c> inside a quoted scalar would be read here as a
/// comment; the consequence is a refusal to rewrite in place, never a silent loss, and baseline files
/// are generated documents where that shape does not occur.
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
            if (IsComment(lines[index]))
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

    private static bool IsComment(string line)
    {
        return line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);
    }

    private static bool IsCommentOrBlank(string line)
    {
        return line.AsSpan().IsWhiteSpace() || IsComment(line);
    }
}
