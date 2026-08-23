using System.Buffers.Binary;
using System.IO;

namespace ArchLinterNet.Core.History.Git;

// Version-2 pack index lookup. The fanout table narrows the search to one first-byte bucket and the
// remaining names are sorted, so a binary search over raw digest bytes locates a pack offset without
// materializing the whole index as objects.
internal sealed class GitPackIndex
{
    private static readonly byte[] _magic = [0xFF, 0x74, 0x4F, 0x63];

    private readonly byte[] _content;
    private readonly int _digestLength;
    private readonly int _count;
    private readonly int _namesOffset;
    private readonly int _smallOffsetsOffset;
    private readonly int _largeOffsetsOffset;

    private GitPackIndex(byte[] content, int digestLength, int count, int namesOffset, int smallOffsetsOffset, int largeOffsetsOffset)
    {
        _content = content;
        _digestLength = digestLength;
        _count = count;
        _namesOffset = namesOffset;
        _smallOffsetsOffset = smallOffsetsOffset;
        _largeOffsetsOffset = largeOffsetsOffset;
    }

    public static GitPackIndex Load(string indexPath, int digestLength)
    {
        byte[] content = File.ReadAllBytes(indexPath);
        return Parse(content, indexPath, digestLength);
    }

    internal static GitPackIndex Load(byte[] content, int digestLength)
        => HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic pack index could not be read",
            objectId: null,
            path: null,
            read: () => Parse(content, "<synthetic>", digestLength));

    private static GitPackIndex Parse(byte[] content, string indexPath, int digestLength)
    {
        const int FanoutOffset = 8;
        const int FanoutLength = 256 * 4;
        if (content.Length < FanoutOffset + FanoutLength
            || !content.AsSpan(0, 4).SequenceEqual(_magic)
            || BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(4, 4)) != 2)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"The pack index '{indexPath}' is not a supported version-2 pack index.",
                path: indexPath);
        }

        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(FanoutOffset + FanoutLength - 4, 4)));
        long namesOffsetLong = FanoutOffset + FanoutLength;

        // Long arithmetic here on purpose: a corrupt or hostile index can declare a `count` large
        // enough that the int-sized layout math below would silently wrap instead of exceeding the
        // file's actual length, which would then pass the bounds check on a wrapped-around value.
        long smallOffsetsOffsetLong = namesOffsetLong + ((long)count * digestLength) + ((long)count * 4);
        long largeOffsetsOffsetLong = smallOffsetsOffsetLong + ((long)count * 4);
        if (content.Length < largeOffsetsOffsetLong)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"The pack index '{indexPath}' is truncated.",
                path: indexPath);
        }

        // These casts cannot overflow: the check above already proved each long offset is no larger
        // than content.Length, which is itself a valid array length and therefore fits in an int.
        int namesOffset = checked((int)namesOffsetLong);
        int smallOffsetsOffset = checked((int)smallOffsetsOffsetLong);
        int largeOffsetsOffset = checked((int)largeOffsetsOffsetLong);
        return new GitPackIndex(content, digestLength, count, namesOffset, smallOffsetsOffset, largeOffsetsOffset);
    }

    public bool TryFindOffset(GitObjectId id, out long offset)
    {
        offset = 0;
        ReadOnlySpan<byte> digest = id.Bytes;
        if (digest.Length != _digestLength || _count == 0)
        {
            return false;
        }

        int low = digest[0] == 0 ? 0 : ReadFanout(digest[0] - 1);
        int high = ReadFanout(digest[0]);
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            int comparison = NameAt(middle).SequenceCompareTo(digest);
            if (comparison == 0)
            {
                offset = OffsetAt(middle);
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return false;
    }

    private int ReadFanout(int bucket)
        => checked((int)BinaryPrimitives.ReadUInt32BigEndian(_content.AsSpan(8 + (bucket * 4), 4)));

    private ReadOnlySpan<byte> NameAt(int index)
        => _content.AsSpan(_namesOffset + (index * _digestLength), _digestLength);

    private long OffsetAt(int index)
    {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(_content.AsSpan(_smallOffsetsOffset + (index * 4), 4));
        if ((value & 0x80000000u) == 0)
        {
            return value;
        }

        int largeIndex = (int)(value & 0x7FFFFFFFu);
        int position = _largeOffsetsOffset + (largeIndex * 8);
        if (position + 8 > _content.Length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A pack index large-offset entry is out of range.");
        }

        return checked((long)BinaryPrimitives.ReadUInt64BigEndian(_content.AsSpan(position, 8)));
    }
}
