using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Strict UTF-8 Git paths with no normalization and no locale fallback. Canonical scalar ordering is
// owned by History.Canonical so reporting can share it without importing this raw decoder.
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

}
