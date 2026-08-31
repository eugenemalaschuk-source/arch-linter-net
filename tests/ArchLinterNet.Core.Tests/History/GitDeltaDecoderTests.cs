using System.Text;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Git;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

// Direct unit tests over the packfile delta instruction stream. Real repacked fixtures exercise the
// common copy/insert shapes organically (see HistoryRefResolutionTests), but the structural
// fail-closed paths — reserved instructions, out-of-bounds copies, truncated streams, oversized
// varints — need deltas hand-built to trigger them, since a real git pack never produces one.
[TestFixture]
public sealed class GitDeltaDecoderTests
{
    [Test]
    public void CopyAndInsertReconstructTheExpectedResult()
    {
        byte[] baseContent = Ascii("The quick brown fox jumps over the lazy dog.");
        byte[] delta = BuildDelta(
            baseContent.Length,
            "quick brown-ish fox".Length,
            BuildCopy(4, 5),
            BuildInsert(Ascii(" brown-ish ")),
            BuildCopy(16, 3));

        byte[] result = GitDeltaDecoder.Apply(baseContent, delta);

        Assert.That(Encoding.ASCII.GetString(result), Is.EqualTo("quick brown-ish fox"));
    }

    [Test]
    public void CopyInstructionFlagsForZeroValuedBytesStillAdvanceThroughEveryOffsetAndSizeByte()
    {
        byte[] baseContent = Ascii("0123456789");
        byte[] delta = BuildDelta(
            baseContent.Length,
            3,
            BuildCopy(2, 3, includeAllOffsetBytes: true, includeAllSizeBytes: true));

        byte[] result = GitDeltaDecoder.Apply(baseContent, delta);

        Assert.That(Encoding.ASCII.GetString(result), Is.EqualTo("234"));
    }

    [Test]
    public void ACopySizeOfZeroDefaultsToSixtyFiveThirtySixBytes()
    {
        byte[] baseContent = new byte[0x10000];
        Array.Fill(baseContent, (byte)0x41);
        byte[] delta = BuildDelta(baseContent.Length, 0x10000, BuildCopy(0, 0, includeAllSizeBytes: false));

        byte[] result = GitDeltaDecoder.Apply(baseContent, delta);

        Assert.That(result.Length, Is.EqualTo(0x10000));
        Assert.That(result, Is.All.EqualTo((byte)0x41));
    }

    [Test]
    public void ABaseSizeMismatchFailsClosed()
    {
        byte[] delta = BuildDelta(999, 0);

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void AReservedZeroInstructionFailsClosed()
    {
        byte[] delta = BuildDelta(0, 0, [0x00]);

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void ACopyPastTheEndOfTheBaseFailsClosed()
    {
        byte[] baseContent = Ascii("short");
        byte[] delta = BuildDelta(baseContent.Length, 10, BuildCopy(2, 10));

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply(baseContent, delta));
    }

    [Test]
    public void ACopyPastTheDeclaredResultSizeFailsClosed()
    {
        byte[] baseContent = Ascii("0123456789");
        byte[] delta = BuildDelta(baseContent.Length, 2, BuildCopy(0, 5));

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply(baseContent, delta));
    }

    [Test]
    public void AnInsertPastTheEndOfTheDeltaStreamFailsClosed()
    {
        // Declares a five-byte literal insert but supplies only two payload bytes.
        byte[] delta = [.. BuildDelta(0, 5), 0x05, 0x41, 0x42];

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void AnInsertPastTheDeclaredResultSizeFailsClosed()
    {
        byte[] delta = [.. BuildDelta(0, 2), .. BuildInsert(Ascii("abc"))];

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void FewerProducedBytesThanTheDeclaredResultSizeFailsClosed()
    {
        byte[] delta = [.. BuildDelta(0, 5), .. BuildInsert(Ascii("ab"))];

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void AnOversizedHeaderVarintFailsClosed()
    {
        byte[] delta = Enumerable.Repeat((byte)0x80, 9).ToArray();

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    [Test]
    public void ADeltaThatEndsMidInstructionFailsClosed()
    {
        // A copy instruction flag claiming an offset byte follows, with nothing after it.
        byte[] delta = [.. BuildDelta(0, 0), 0x81];

        AssertFailsWithObjectMalformed(() => GitDeltaDecoder.Apply([], delta));
    }

    private static void AssertFailsWithObjectMalformed(Action action)
    {
        HistoryFailureException failure = Assert.Throws<HistoryFailureException>(action)!;
        Assert.That(((HistoryDiagnostic)failure.Diagnostic).KindText, Is.EqualTo("object_malformed"));
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

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

    // Mirrors the packfile copy-instruction encoding: a leading flag byte with the high bit set,
    // followed by whichever offset/size bytes the flag bits select. `includeAll*` forces every
    // optional byte to be present even when its value is zero, to exercise every flag-bit branch.
    private static byte[] BuildCopy(long offset, long size, bool includeAllOffsetBytes = false, bool includeAllSizeBytes = false)
    {
        byte flags = 0x80;
        List<byte> offsetBytes = [];
        for (int index = 0; index < 4; index++)
        {
            byte value = (byte)((offset >> (index * 8)) & 0xFF);
            if (includeAllOffsetBytes || value != 0)
            {
                flags |= (byte)(1 << index);
                offsetBytes.Add(value);
            }
        }

        List<byte> sizeBytes = [];
        for (int index = 0; index < 3; index++)
        {
            byte value = (byte)((size >> (index * 8)) & 0xFF);
            if (includeAllSizeBytes || value != 0)
            {
                flags |= (byte)(0x10 << index);
                sizeBytes.Add(value);
            }
        }

        return [flags, .. offsetBytes, .. sizeBytes];
    }

    private static byte[] BuildInsert(byte[] literal) => [(byte)literal.Length, .. literal];
}
