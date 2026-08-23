namespace ArchLinterNet.Core.History.Git;

internal readonly record struct GitPackEntryHeader(
    int Type,
    long Size,
    long DataOffset,
    long BaseOffset,
    GitObjectId BaseId);

// Reads the variable-length header shared by file-backed pack access and the bounded synthetic
// fuzzing seam. The callbacks keep normal pack reads streaming while the byte-array overload
// prevents the seam from opening or discovering a repository.
internal static class GitPackEntryHeaderParser
{
    private const int TypeCommit = 1;
    private const int TypeOffsetDelta = 6;
    private const int TypeReferenceDelta = 7;

    internal static GitPackEntryHeader Read(
        long offset,
        int digestLength,
        Func<long, byte> readByte,
        Func<long, int, byte[]> readExactly)
    {
        long position = offset;
        byte current = readByte(position++);
        int type = (current >> 4) & 0x07;
        long size = current & 0x0F;
        int shift = 4;
        while ((current & 0x80) != 0)
        {
            current = readByte(position++);
            size |= (long)(current & 0x7F) << shift;
            shift += 7;
        }

        long baseOffset = 0;
        GitObjectId baseId = default;
        if (type == TypeOffsetDelta)
        {
            current = readByte(position++);
            long relative = current & 0x7F;
            while ((current & 0x80) != 0)
            {
                current = readByte(position++);
                relative = ((relative + 1) << 7) | (uint)(current & 0x7F);
            }

            baseOffset = offset - relative;
            if (baseOffset < 0)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMalformed,
                    "A packfile offset delta points before the start of its pack.");
            }
        }
        else if (type == TypeReferenceDelta)
        {
            byte[] digest = readExactly(position, digestLength);
            position += digestLength;
            baseId = GitObjectId.FromBytes(digest);
        }
        else if (type is < TypeCommit or > 4)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"A packfile entry declares unsupported object type {type}.");
        }

        return new GitPackEntryHeader(type, size, position, baseOffset, baseId);
    }

    internal static GitPackEntryHeader Read(byte[] content, int digestLength)
        => Read(
            offset: 0,
            digestLength,
            readByte: position => ReadByte(content, position),
            readExactly: (position, length) => ReadExactly(content, position, length));

    private static byte ReadByte(byte[] content, long position)
    {
        if (position < 0 || position >= content.LongLength)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic packfile entry header runs past the supplied bytes.");
        }

        return content[(int)position];
    }

    private static byte[] ReadExactly(byte[] content, long position, int length)
    {
        if (position < 0 || length < 0 || position > content.LongLength - length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic packfile reference header runs past the supplied bytes.");
        }

        return content.AsSpan((int)position, length).ToArray();
    }
}
