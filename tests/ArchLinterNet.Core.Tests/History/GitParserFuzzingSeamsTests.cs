using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Git;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class GitParserFuzzingSeamsTests
{
    [Test]
    public void LooseObjectRouteAcceptsACompressedCanonicalObject()
    {
        byte[] inflated = Encoding.ASCII.GetBytes("blob 5\0hello");

        Assert.DoesNotThrow(() => GitParserFuzzingSeams.Execute([0, .. Compress(inflated)], 20));
    }

    [Test]
    public void LooseObjectRouteFailsClosedForMalformedCompressedBytes()
    {
        AssertFailsClosed([0, 0x00], 20);
    }

    [TestCase(20)]
    [TestCase(32)]
    public void PackIndexRouteFindsTheSyntheticDigest(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0xA5, digestLength).ToArray();
        byte[] index = BuildPackIndex(digest, offset: 0x42);

        Assert.DoesNotThrow(() => GitParserFuzzingSeams.Execute([1, .. digest, .. index], digestLength));
    }

    [TestCase(20)]
    [TestCase(32)]
    public void PackIndexRouteFailsClosedForTruncatedBytes(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0xA5, digestLength).ToArray();

        AssertFailsClosed([1, .. digest, 0xFF], digestLength);
    }

    [TestCase(20)]
    [TestCase(32)]
    public void PackEntryRouteAcceptsAReferenceHeaderInBothDigestModes(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0x3C, digestLength).ToArray();

        Assert.DoesNotThrow(() => GitParserFuzzingSeams.Execute([2, 0x70, .. digest], digestLength));
    }

    [TestCase(20)]
    [TestCase(32)]
    public void PackEntryRouteFailsClosedWhenTheReferenceHeaderIsTruncated(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0x3C, digestLength - 1).ToArray();

        AssertFailsClosed([2, 0x70, .. digest], digestLength);
    }

    [TestCase(20)]
    [TestCase(32)]
    public void ReferenceDeltaRouteReconstructsAgainstTheFixedSyntheticBase(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0x7E, digestLength).ToArray();
        byte[] delta = BuildDelta(baseSize: 4, resultSize: 6, [6, .. Encoding.ASCII.GetBytes("target")]);
        byte[] packedEntry = [0x79, .. digest, .. Compress(delta)];

        Assert.DoesNotThrow(() => GitParserFuzzingSeams.Execute([3, .. packedEntry], digestLength));
    }

    [TestCase(20)]
    [TestCase(32)]
    public void ReferenceDeltaRouteFailsClosedForMalformedPayload(int digestLength)
    {
        byte[] digest = Enumerable.Repeat((byte)0x7E, digestLength).ToArray();

        AssertFailsClosed([3, 0x76, .. digest], digestLength);
    }

    [Test]
    public void EmptyAndUnknownRoutesFailClosed()
    {
        AssertFailsClosed([], 20);
        AssertFailsClosed([0xFF], 20);
    }

    private static void AssertFailsClosed(byte[] input, int digestLength)
    {
        HistoryFailureException failure = Assert.Throws<HistoryFailureException>(
            () => GitParserFuzzingSeams.Execute(input, digestLength))!;

        Assert.That(HistoryFailures.DiagnosticOf(failure).Kind, Is.EqualTo(HistoryDiagnosticKind.ObjectMalformed));
    }

    private static byte[] BuildPackIndex(byte[] digest, uint offset)
    {
        const int FanoutOffset = 8;
        const int FanoutLength = 256 * 4;
        const int Count = 1;
        int namesOffset = FanoutOffset + FanoutLength;
        int crcOffset = namesOffset + digest.Length;
        int smallOffsetsOffset = crcOffset + (Count * 4);
        byte[] content = new byte[smallOffsetsOffset + (Count * 4) + 4];
        content[0] = 0xFF;
        content[1] = 0x74;
        content[2] = 0x4F;
        content[3] = 0x63;
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(FanoutOffset + (255 * 4), 4), Count);
        digest.CopyTo(content, namesOffset);
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(smallOffsetsOffset, 4), offset);
        return content;
    }

    private static byte[] BuildDelta(long baseSize, long resultSize, params byte[][] instructions)
        => [.. WriteSizeVarint(baseSize), .. WriteSizeVarint(resultSize), .. instructions.SelectMany(static bytes => bytes)];

    private static byte[] WriteSizeVarint(long value)
    {
        List<byte> bytes = [];
        do
        {
            byte current = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                current |= 0x80;
            }

            bytes.Add(current);
        }
        while (value != 0);

        return [.. bytes];
    }

    private static byte[] Compress(byte[] content)
    {
        using MemoryStream buffer = new();
        using (ZLibStream stream = new(buffer, CompressionMode.Compress, leaveOpen: true))
        {
            stream.Write(content);
        }

        return buffer.ToArray();
    }
}
