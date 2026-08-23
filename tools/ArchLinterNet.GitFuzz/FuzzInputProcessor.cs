using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.GitFuzz;

internal static class FuzzInputProcessor
{
    internal const int MaxInputBytes = 1_048_576;

    private static readonly int[] _sha1Only = [20];
    private static readonly int[] _sha1AndSha256 = [20, 32];

    internal static FuzzExecutionResult Execute(Stream stream)
    {
        byte[]? input = ReadBounded(stream);
        return input is null
            ? new FuzzExecutionResult(FuzzExecutionOutcome.Oversized, 0, 0)
            : Execute(input);
    }

    internal static FuzzExecutionResult Execute(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > MaxInputBytes)
        {
            return new FuzzExecutionResult(FuzzExecutionOutcome.Oversized, 0, 0);
        }

        int canonical = 0;
        int failClosed = 0;
        foreach (int digestLength in DigestLengthsFor(input))
        {
            if (ExecuteDigest(input, digestLength))
            {
                canonical++;
            }
            else
            {
                failClosed++;
            }
        }

        FuzzExecutionOutcome outcome = canonical > 0
            ? FuzzExecutionOutcome.Canonical
            : FuzzExecutionOutcome.FailClosed;
        return new FuzzExecutionResult(outcome, canonical, failClosed);
    }

    private static int[] DigestLengthsFor(byte[] input)
        => input.Length > 0 && input[0] is 1 or 2 or 3
            ? _sha1AndSha256
            : _sha1Only;

    private static bool ExecuteDigest(byte[] input, int digestLength)
    {
        try
        {
            GitParserFuzzingSeams.Execute(input, digestLength);
            return true;
        }
        catch (HistoryFailureException)
        {
            return false;
        }
    }

    private static byte[]? ReadBounded(Stream stream)
    {
        byte[] buffer = new byte[8192];
        using MemoryStream content = new();
        while (true)
        {
            int remaining = MaxInputBytes + 1 - checked((int)content.Length);
            if (remaining == 0)
            {
                return null;
            }

            int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                return content.ToArray();
            }

            content.Write(buffer, 0, read);
            if (content.Length > MaxInputBytes)
            {
                return null;
            }
        }
    }
}
