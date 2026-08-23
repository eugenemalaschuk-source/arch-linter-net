using System.Buffers.Binary;

namespace ArchLinterNet.Core.History.Git;

// Internal, byte-array-only entry point for the selected parser seams. The caller owns input
// acquisition and bounds; this adapter never discovers a repository or handles an external path.
internal static class GitParserFuzzingSeams
{
    private const int Sha1DigestLength = 20;
    private const int Sha256DigestLength = 32;
    private const int TypeReferenceDelta = 7;
    private const int TypeOffsetDelta = 6;
    private static readonly byte[] _syntheticBase = "base"u8.ToArray();

    internal static void Execute(byte[] input, int digestLength)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateDigestLength(digestLength);

        if (input.Length == 0)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic Git parser input must begin with a route selector.");
        }

        switch (input[0])
        {
            case 0:
                ParseLoose(input);
                return;
            case 1:
                ParsePackIndex(input, digestLength);
                return;
            case 2:
                ParsePackEntryHeader(input, digestLength);
                return;
            case 3:
                ParseReferenceDelta(input, digestLength);
                return;
            case 4:
                ParseOffsetDelta(input, digestLength);
                return;
            default:
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMalformed,
                    $"A synthetic Git parser input declares unsupported route selector {input[0]}.");
        }
    }

    private static void ParseLoose(byte[] input)
    {
        _ = GitObjectDatabase.ParseLooseBytes(input.AsSpan(1).ToArray());
    }

    private static void ParsePackIndex(byte[] input, int digestLength)
    {
        if (input.Length < 1 + digestLength)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic pack-index input is missing its lookup digest.");
        }

        byte[] digest = input.AsSpan(1, digestLength).ToArray();
        byte[] indexContent = input.AsSpan(1 + digestLength).ToArray();
        GitPackIndex index = GitPackIndex.Load(indexContent, digestLength);
        GitObjectId target = GitObjectId.FromBytes(digest);
        _ = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic pack-index lookup could not be completed",
            objectId: target.Hex,
            path: null,
            read: () => index.TryFindOffset(target, out _));
    }

    private static void ParsePackEntryHeader(byte[] input, int digestLength)
    {
        _ = GitPackEntryHeaderParser.Read(input.AsSpan(1).ToArray(), digestLength);
    }

    private static void ParseReferenceDelta(byte[] input, int digestLength)
    {
        byte[] packedContent = input.AsSpan(1).ToArray();
        GitPackEntryHeader header = GitPackEntryHeaderParser.Read(packedContent, digestLength);
        if (header.Type != TypeReferenceDelta)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic REF-delta input does not contain an OBJ_REF_DELTA header.");
        }

        byte[] delta = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic REF-delta payload could not be inflated",
            objectId: header.BaseId.Hex,
            path: null,
            read: () => GitPackPayloadInflater.Inflate(packedContent, header.DataOffset, header.Size));
        _ = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic REF-delta could not be reconstructed",
            objectId: header.BaseId.Hex,
            path: null,
            read: () => GitDeltaDecoder.Apply(_syntheticBase, delta));
    }

    private static void ParseOffsetDelta(byte[] input, int digestLength)
    {
        const int EnvelopeLength = 1 + sizeof(int);
        if (input.Length < EnvelopeLength)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic OFS-delta input is missing its delta-entry offset.");
        }

        int deltaOffset = BinaryPrimitives.ReadInt32BigEndian(input.AsSpan(1, sizeof(int)));
        byte[] packContent = input.AsSpan(EnvelopeLength).ToArray();
        if (deltaOffset <= 0 || deltaOffset >= packContent.Length)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic OFS-delta input declares an invalid delta-entry offset.");
        }

        GitPackEntryHeader baseHeader = GitPackEntryHeaderParser.Read(packContent, digestLength);
        if (baseHeader.Type is TypeOffsetDelta or TypeReferenceDelta)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic OFS-delta base entry must be a non-delta object.");
        }

        byte[] baseContent = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic OFS-delta base payload could not be inflated",
            objectId: null,
            path: null,
            read: () => GitPackPayloadInflater.Inflate(packContent, baseHeader.DataOffset, baseHeader.Size));

        GitPackEntryHeader header = GitPackEntryHeaderParser.Read(packContent, deltaOffset, digestLength);
        if (header.Type != TypeOffsetDelta || header.BaseOffset != 0)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A synthetic OFS-delta input does not point at its synthetic base entry.");
        }

        byte[] delta = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic OFS-delta payload could not be inflated",
            objectId: null,
            path: null,
            read: () => GitPackPayloadInflater.Inflate(packContent, header.DataOffset, header.Size));
        _ = HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            "The synthetic OFS-delta could not be reconstructed",
            objectId: null,
            path: null,
            read: () => GitDeltaDecoder.Apply(baseContent, delta));
    }

    private static void ValidateDigestLength(int digestLength)
    {
        if (digestLength is not (Sha1DigestLength or Sha256DigestLength))
        {
            throw new ArgumentOutOfRangeException(
                nameof(digestLength),
                digestLength,
                "Git parser fuzzing seams support only 20-byte SHA-1 or 32-byte SHA-256 digests.");
        }
    }
}
