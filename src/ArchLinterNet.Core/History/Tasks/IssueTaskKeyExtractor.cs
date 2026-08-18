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
        for (int index = 0; index < rawMessage.Length; index++)
        {
            if (rawMessage[index] != (byte)'#')
            {
                continue;
            }

            if (index > 0 && IsBoundaryExcluded(rawMessage[index - 1]))
            {
                continue;
            }

            int digitsStart = index + 1;
            int digitsEnd = digitsStart;
            while (digitsEnd < rawMessage.Length && rawMessage[digitsEnd] is >= (byte)'0' and <= (byte)'9')
            {
                digitsEnd++;
            }

            if (digitsEnd == digitsStart || (digitsEnd < rawMessage.Length && IsBoundaryExcluded(rawMessage[digitsEnd])))
            {
                continue;
            }

            string digits = Encoding.ASCII.GetString(rawMessage, digitsStart, digitsEnd - digitsStart);
            BigInteger id = BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            if (id > BigInteger.Zero)
            {
                matches.Add(new TaskKeyMatch(
                    ExtractorId,
                    new TaskKey(Namespace, id),
                    index,
                    digitsEnd,
                    Encoding.ASCII.GetString(rawMessage, index, digitsEnd - index)));
            }

            // Continue from the final digit so `#12#13` is rejected by the trailing boundary check
            // on both candidates rather than by scan position.
            index = digitsEnd - 1;
        }
    }

    private static bool IsBoundaryExcluded(byte value)
        => value is (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'a' and <= (byte)'z')
            or (>= (byte)'0' and <= (byte)'9')
            or (byte)'_'
            or (byte)'#';
}
