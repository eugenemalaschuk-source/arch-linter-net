namespace ArchLinterNet.Core.History.Git;

// Canonical object identity for release forensics: the full digest rendered as lowercase ASCII
// hexadecimal, two characters per digest byte. Abbreviated IDs are deliberately not representable,
// so no code path can accidentally introduce a non-canonical operand or evidence ID.
internal readonly struct GitObjectId : IEquatable<GitObjectId>, IComparable<GitObjectId>
{
    private const string HexDigits = "0123456789abcdef";

    private readonly byte[]? _bytes;
    private readonly string? _hex;

    private GitObjectId(byte[] bytes, string hex)
    {
        _bytes = bytes;
        _hex = hex;
    }

    public bool IsEmpty => _bytes is null;

    public string Hex => _hex ?? string.Empty;

    public int DigestLength => _bytes?.Length ?? 0;

    public ReadOnlySpan<byte> Bytes => _bytes is null ? ReadOnlySpan<byte>.Empty : _bytes;

    public static GitObjectId FromBytes(ReadOnlySpan<byte> digest)
    {
        byte[] copy = digest.ToArray();
        return new GitObjectId(copy, ToHex(copy));
    }

    public static bool TryParseHex(string? text, int digestLength, out GitObjectId id)
    {
        id = default;
        if (text is null || text.Length != digestLength * 2)
        {
            return false;
        }

        byte[] bytes = new byte[digestLength];
        for (int index = 0; index < digestLength; index++)
        {
            if (!TryReadNibble(text[index * 2], out int high) || !TryReadNibble(text[(index * 2) + 1], out int low))
            {
                return false;
            }

            bytes[index] = (byte)((high << 4) | low);
        }

        id = new GitObjectId(bytes, ToHex(bytes));
        return true;
    }

    public bool Equals(GitObjectId other) => string.Equals(Hex, other.Hex, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is GitObjectId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Hex);

    public int CompareTo(GitObjectId other) => string.CompareOrdinal(Hex, other.Hex);

    public static bool operator <(GitObjectId left, GitObjectId right) => left.CompareTo(right) < 0;

    public static bool operator <=(GitObjectId left, GitObjectId right) => left.CompareTo(right) <= 0;

    public static bool operator >(GitObjectId left, GitObjectId right) => left.CompareTo(right) > 0;

    public static bool operator >=(GitObjectId left, GitObjectId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Hex;

    private static string ToHex(byte[] bytes)
    {
        char[] characters = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            characters[index * 2] = HexDigits[bytes[index] >> 4];
            characters[(index * 2) + 1] = HexDigits[bytes[index] & 0x0F];
        }

        return new string(characters);
    }

    private static bool TryReadNibble(char character, out int value)
    {
        // Authored operands may arrive uppercase; canonical retention is always lowercase.
        value = character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1,
        };

        return value >= 0;
    }
}
