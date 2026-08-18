using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Strict UTF-8 Git paths with no normalization and no locale fallback. Canonical ordering is by
// Unicode scalar value, which is deliberately not the host's UTF-16 string ordering: a supplementary
// scalar must sort above every BMP scalar.
internal static class GitPathDecoder
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string DecodeSegment(byte[] nameBytes, string parentPath, string commitId)
    {
        try
        {
            return _strictUtf8.GetString(nameBytes);
        }
        catch (DecoderFallbackException)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.PathEncodingInvalid,
                $"A Git path segment below '{(parentPath.Length == 0 ? "<root>" : parentPath)}' in commit '{commitId}' is not valid UTF-8.",
                objectId: commitId,
                path: parentPath);
        }
    }

    public static int CompareScalarValue(string left, string right)
    {
        int leftIndex = 0;
        int rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            int leftScalar = NextScalar(left, ref leftIndex);
            int rightScalar = NextScalar(right, ref rightIndex);
            if (leftScalar != rightScalar)
            {
                return leftScalar < rightScalar ? -1 : 1;
            }
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int NextScalar(string value, ref int index)
    {
        char current = value[index];
        if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
        {
            int scalar = char.ConvertToUtf32(current, value[index + 1]);
            index += 2;
            return scalar;
        }

        index++;
        return current;
    }
}
