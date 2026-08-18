namespace ArchLinterNet.Core.History.Git;

// Packfile delta reconstruction. Copy instructions address the base object and insert instructions
// carry literal bytes; a delta whose declared sizes do not match what it actually reconstructs is a
// corrupt object rather than a recoverable approximation.
internal static class GitDeltaDecoder
{
    public static byte[] Apply(byte[] baseContent, byte[] delta)
    {
        int position = 0;
        long declaredBaseSize = ReadSizeVarint(delta, ref position);
        long declaredResultSize = ReadSizeVarint(delta, ref position);
        if (declaredBaseSize != baseContent.LongLength)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile delta declares a base size that does not match its base object.");
        }

        byte[] result = new byte[checked((int)declaredResultSize)];
        int written = 0;
        while (position < delta.Length)
        {
            byte instruction = delta[position++];
            if ((instruction & 0x80) != 0)
            {
                CopyFromBase(baseContent, delta, ref position, instruction, result, ref written);
                continue;
            }

            if (instruction == 0)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMalformed,
                    "A packfile delta contains a reserved zero instruction.");
            }

            Insert(delta, ref position, instruction, result, ref written);
        }

        if (written != result.Length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile delta produced fewer bytes than its declared result size.");
        }

        return result;
    }

    private static void CopyFromBase(byte[] baseContent, byte[] delta, ref int position, byte instruction, byte[] result, ref int written)
    {
        long offset = 0;
        long size = 0;
        for (int shift = 0; shift < 32; shift += 8)
        {
            if ((instruction & (1 << (shift / 8))) != 0)
            {
                offset |= (long)ReadByte(delta, ref position) << shift;
            }
        }

        for (int shift = 0; shift < 24; shift += 8)
        {
            if ((instruction & (0x10 << (shift / 8))) != 0)
            {
                size |= (long)ReadByte(delta, ref position) << shift;
            }
        }

        if (size == 0)
        {
            size = 0x10000;
        }

        if (offset < 0 || size < 0 || offset + size > baseContent.LongLength || written + size > result.LongLength)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile delta copy instruction addresses bytes outside its base or result object.");
        }

        Array.Copy(baseContent, (int)offset, result, written, (int)size);
        written += (int)size;
    }

    private static void Insert(byte[] delta, ref int position, byte length, byte[] result, ref int written)
    {
        if (position + length > delta.Length || written + length > result.Length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile delta insert instruction runs past the delta or result object.");
        }

        Array.Copy(delta, position, result, written, length);
        position += length;
        written += length;
    }

    private static long ReadSizeVarint(byte[] delta, ref int position)
    {
        long value = 0;
        int shift = 0;
        while (true)
        {
            byte current = ReadByte(delta, ref position);
            value |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
            if (shift > 56)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMalformed,
                    "A packfile delta size varint is out of range.");
            }
        }
    }

    private static byte ReadByte(byte[] delta, ref int position)
    {
        if (position >= delta.Length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile delta ended in the middle of an instruction.");
        }

        return delta[position++];
    }
}
