using System.Globalization;
using System.Numerics;
using System.Text;

namespace ArchLinterNet.Core.History.Git;

// Exact `author`/`committer` grammar parsed right to left from raw bytes. Delegating this to a Git
// library's identity formatter is what would reintroduce locale decoding and calendar conversion, so
// anything that does not parse uniquely by this rule fails closed instead.
internal sealed class GitIdentityHeader
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private GitIdentityHeader(byte[] name, byte[] email, BigInteger epochSecond, string timezoneToken)
    {
        NameBytes = name;
        EmailBytes = email;
        EpochSecond = epochSecond;
        TimezoneToken = timezoneToken;
    }

    public byte[] NameBytes { get; }

    public byte[] EmailBytes { get; }

    // Arbitrary precision on purpose: a valid commit may carry an epoch second outside any host
    // calendar range, and the timezone token never shifts this value.
    public BigInteger EpochSecond { get; }

    public string TimezoneToken { get; }

    public static GitIdentityHeader Parse(string headerName, byte[] value, string commitId)
    {
        int timezoneSeparator = Array.LastIndexOf(value, (byte)' ');
        if (timezoneSeparator <= 0)
        {
            throw Malformed(headerName, commitId);
        }

        int timestampSeparator = Array.LastIndexOf(value, (byte)' ', timezoneSeparator - 1);
        if (timestampSeparator <= 0)
        {
            throw Malformed(headerName, commitId);
        }

        string timezoneToken = Encoding.ASCII.GetString(value, timezoneSeparator + 1, value.Length - timezoneSeparator - 1);
        string timestampToken = Encoding.ASCII.GetString(value, timestampSeparator + 1, timezoneSeparator - timestampSeparator - 1);
        if (!IsTimezoneToken(timezoneToken) || !TryParseTimestamp(timestampToken, out BigInteger epochSecond))
        {
            throw Malformed(headerName, commitId);
        }

        (byte[] name, byte[] email) = SplitIdentity(value.AsSpan(0, timestampSeparator), headerName, commitId);
        return new GitIdentityHeader(name, email, epochSecond, timezoneToken);
    }

    // Canonical author identity: prefer the trimmed email, fall back to the trimmed name, decode
    // strictly, and lowercase only ASCII A-Z so two runtimes with different culture casing agree.
    public string CanonicalIdentity(string commitId)
    {
        byte[] selected = Trim(EmailBytes);
        if (selected.Length == 0)
        {
            selected = Trim(NameBytes);
        }

        if (selected.Length == 0)
        {
            return "unknown";
        }

        string decoded;
        try
        {
            decoded = _strictUtf8.GetString(selected);
        }
        catch (DecoderFallbackException)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.AuthorEncodingInvalid,
                $"The selected author identity bytes of commit '{commitId}' are not valid UTF-8.",
                objectId: commitId);
        }

        return LowercaseAscii(decoded.Trim(' ', '\t'));
    }

    public string EpochSecondText => EpochSecond.ToString(CultureInfo.InvariantCulture);

    private static (byte[] Name, byte[] Email) SplitIdentity(ReadOnlySpan<byte> identity, string headerName, string commitId)
    {
        if (identity.Length == 0 || identity[^1] != (byte)'>')
        {
            throw Malformed(headerName, commitId);
        }

        int emailStart = identity[..^1].LastIndexOf((byte)'<');
        if (emailStart < 0)
        {
            throw Malformed(headerName, commitId);
        }

        return (identity[..emailStart].ToArray(), identity[(emailStart + 1)..^1].ToArray());
    }

    private static bool IsTimezoneToken(string token)
    {
        if (token.Length != 5 || token[0] is not ('+' or '-'))
        {
            return false;
        }

        for (int index = 1; index < 5; index++)
        {
            if (token[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseTimestamp(string token, out BigInteger value)
    {
        value = BigInteger.Zero;
        int start = token.StartsWith('-') ? 1 : 0;
        if (token.Length <= start)
        {
            return false;
        }

        for (int index = start; index < token.Length; index++)
        {
            if (token[index] is < '0' or > '9')
            {
                return false;
            }
        }

        // Leading zeroes and `-0` are spelling only; BigInteger parsing collapses both.
        return BigInteger.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static byte[] Trim(byte[] bytes)
    {
        int start = 0;
        int end = bytes.Length;
        while (start < end && bytes[start] is 0x20 or 0x09)
        {
            start++;
        }

        while (end > start && bytes[end - 1] is 0x20 or 0x09)
        {
            end--;
        }

        return bytes[start..end];
    }

    private static string LowercaseAscii(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] is >= 'A' and <= 'Z')
            {
                characters[index] = (char)(characters[index] + 32);
            }
        }

        return new string(characters);
    }

    private static HistoryFailureException Malformed(string headerName, string commitId)
        => HistoryFailures.Fail(
            HistoryDiagnosticKind.CommitMetadataMalformed,
            $"The '{headerName}' header of commit '{commitId}' does not match the canonical identity grammar.",
            objectId: commitId);
}
