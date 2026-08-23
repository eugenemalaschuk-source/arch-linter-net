using System.IO;
using System.IO.Compression;

namespace ArchLinterNet.Core.History.Git;

// Shared zlib payload handling for normal pack streams and synthetic byte-array parser seams.
internal static class GitPackPayloadInflater
{
    internal static byte[] Inflate(Stream packed, long size)
    {
        byte[] content = new byte[checked((int)size)];
        using ZLibStream stream = new(packed, CompressionMode.Decompress, leaveOpen: true);
        stream.ReadExactly(content);
        return content;
    }

    internal static byte[] Inflate(byte[] packedContent, long dataOffset, long size)
    {
        using MemoryStream packed = new(packedContent, writable: false);
        packed.Position = dataOffset;
        return Inflate(packed, size);
    }
}
