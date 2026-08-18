using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Structural commit/tag parsing: LF-delimited headers up to the first empty line, then the raw
// payload. Continuation lines (a leading space, used by `gpgsig`) belong to the preceding header and
// never become direct headers, so a multi-line signature cannot masquerade as an `author` line.
internal static class GitHeaderReader
{
    public static IReadOnlyList<GitHeaderLine> ReadDirectHeaders(byte[] payload)
        => Split(payload).Headers;

    public static (IReadOnlyList<GitHeaderLine> Headers, byte[] Message) Split(byte[] payload)
    {
        List<GitHeaderLine> headers = [];
        int position = 0;
        while (position < payload.Length)
        {
            int lineEnd = Array.IndexOf(payload, (byte)'\n', position);
            if (lineEnd < 0)
            {
                // Headers that are never terminated leave no message separator at all.
                AppendHeader(headers, payload, position, payload.Length);
                return (headers, []);
            }

            if (lineEnd == position)
            {
                return (headers, payload[(position + 1)..]);
            }

            AppendHeader(headers, payload, position, lineEnd);
            position = lineEnd + 1;
        }

        return (headers, []);
    }

    private static void AppendHeader(List<GitHeaderLine> headers, byte[] payload, int start, int end)
    {
        if (payload[start] == (byte)' ')
        {
            return;
        }

        int space = Array.IndexOf(payload, (byte)' ', start, end - start);
        if (space < 0)
        {
            headers.Add(new GitHeaderLine(Encoding.ASCII.GetString(payload, start, end - start), []));
            return;
        }

        headers.Add(new GitHeaderLine(
            Encoding.ASCII.GetString(payload, start, space - start),
            payload[(space + 1)..end]));
    }
}
