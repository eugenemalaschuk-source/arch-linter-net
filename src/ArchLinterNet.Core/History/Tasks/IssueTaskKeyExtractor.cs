using System.Numerics;
using System.Text;
using ArchLinterNet.Core.History.Tasks.Abstractions;

namespace ArchLinterNet.Core.History.Tasks;

// The default `issue` extractor: literal `#` plus ASCII digits with a value greater than zero, where
// the scalars immediately around the match must be outside `[A-Za-z0-9_#]`.
//
// Scanning raw bytes rather than decoded characters is what makes the byte spans exact. It is safe
// because every boundary-relevant scalar is ASCII: any non-ASCII byte belongs to a multi-byte scalar
// and is therefore outside the boundary set by construction.
internal sealed class IssueTaskKeyExtractor : ITaskKeyExtractor
{
    public const string Namespace = "issue";

    public string ExtractorId => "issue";

    public void Extract(byte[] rawMessage, ICollection<TaskKeyMatch> matches)
    {
        int index = 0;
        while (index < rawMessage.Length)
        {
            bool precededByBoundaryExcluded = index > 0 && IsBoundaryExcluded(rawMessage[index - 1]);
            if (rawMessage[index] != (byte)'#' || precededByBoundaryExcluded)
            {
                index++;
                continue;
            }

            int digitsEnd = ScanDigits(rawMessage, index + 1);
            if (!IsValidMatch(rawMessage, index, digitsEnd))
            {
                index++;
                continue;
            }

            AddMatch(matches, rawMessage, index, digitsEnd);

            // Continue from the final digit so `#12#13` is rejected by the trailing boundary check
            // on both candidates rather than by scan position.
            index = digitsEnd;
        }
    }

    private static int ScanDigits(byte[] rawMessage, int start)
    {
        int end = start;
        while (end < rawMessage.Length && rawMessage[end] is >= (byte)'0' and <= (byte)'9')
        {
            end++;
        }

        return end;
    }

    private static bool IsValidMatch(byte[] rawMessage, int hashIndex, int digitsEnd)
    {
        int digitsStart = hashIndex + 1;
        bool hasDigits = digitsEnd > digitsStart;
        bool trailingBoundaryExcluded = digitsEnd < rawMessage.Length && IsBoundaryExcluded(rawMessage[digitsEnd]);
        return hasDigits && !trailingBoundaryExcluded;
    }

    private void AddMatch(ICollection<TaskKeyMatch> matches, byte[] rawMessage, int hashIndex, int digitsEnd)
    {
        int digitsStart = hashIndex + 1;
        string digits = Encoding.ASCII.GetString(rawMessage, digitsStart, digitsEnd - digitsStart);
        BigInteger id = BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        if (id > BigInteger.Zero)
        {
            matches.Add(new TaskKeyMatch(
                ExtractorId,
                new TaskKey(Namespace, id),
                hashIndex,
                digitsEnd,
                Encoding.ASCII.GetString(rawMessage, hashIndex, digitsEnd - hashIndex)));
        }
    }

    private static bool IsBoundaryExcluded(byte value)
        => value is (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'a' and <= (byte)'z')
            or (>= (byte)'0' and <= (byte)'9')
            or (byte)'_'
            or (byte)'#';
}
