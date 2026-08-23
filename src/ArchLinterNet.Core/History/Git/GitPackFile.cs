using System.IO;

namespace ArchLinterNet.Core.History.Git;

// One `.pack`/`.idx` pair. Base objects reached through `OBJ_OFS_DELTA`/`OBJ_REF_DELTA` are cached
// by pack offset because long delta chains otherwise re-inflate the same base once per link.
internal sealed class GitPackFile : IDisposable
{
    private const int TypeOffsetDelta = 6;
    private const int TypeReferenceDelta = 7;
    private const int BaseCacheCapacity = 256;

    private readonly Dictionary<long, GitRawObject> _baseCache = [];
    private readonly Queue<long> _baseCacheOrder = new();
    private readonly GitPackIndex _index;
    private readonly FileStream _pack;
    private readonly int _digestLength;
    private readonly Func<GitObjectId, GitRawObject?> _resolveExternalBase;

    public GitPackFile(string packPath, string indexPath, int digestLength, Func<GitObjectId, GitRawObject?> resolveExternalBase)
    {
        _index = GitPackIndex.Load(indexPath, digestLength);
        _pack = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _digestLength = digestLength;
        _resolveExternalBase = resolveExternalBase;
    }

    public GitRawObject? TryRead(GitObjectId id)
        => HistoryFailures.WrapObjectAccess(
            HistoryDiagnosticKind.ObjectMalformed,
            $"The packfile object '{id.Hex}' could not be read",
            objectId: id.Hex,
            path: null,
            read: () => _index.TryFindOffset(id, out long offset) ? ReadAt(offset) : null);

    public void Dispose() => _pack.Dispose();

    private GitRawObject ReadAt(long offset)
    {
        GitPackEntryHeader header = ReadEntryHeader(offset);
        int type = header.Type;
        long size = header.Size;
        long dataOffset = header.DataOffset;
        long baseOffset = header.BaseOffset;
        GitObjectId baseId = header.BaseId;
        byte[] content = Inflate(dataOffset, size);
        if (type is not (TypeOffsetDelta or TypeReferenceDelta))
        {
            return new GitRawObject(ToKind(type), content);
        }

        (GitObjectKind baseKind, byte[] baseContent) = type == TypeOffsetDelta
            ? ReadBase(baseOffset)
            : ReadExternalBase(baseId);
        return new GitRawObject(baseKind, GitDeltaDecoder.Apply(baseContent, content));
    }

    private (GitObjectKind Kind, byte[] Content) ReadBase(long offset)
    {
        // A base can itself be a delta, so the resolved kind is cached with the payload rather than
        // re-derived from the entry header, which would only report the delta type.
        if (_baseCache.TryGetValue(offset, out GitRawObject? cached))
        {
            return (cached.Kind, cached.Payload);
        }

        GitRawObject resolved = ReadAt(offset);
        _baseCache[offset] = resolved;
        _baseCacheOrder.Enqueue(offset);
        if (_baseCacheOrder.Count > BaseCacheCapacity)
        {
            _baseCache.Remove(_baseCacheOrder.Dequeue());
        }

        return (resolved.Kind, resolved.Payload);
    }

    private (GitObjectKind Kind, byte[] Content) ReadExternalBase(GitObjectId baseId)
    {
        GitRawObject? resolved = TryRead(baseId) ?? _resolveExternalBase(baseId);
        return resolved is null
            ? throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMissing,
                $"The packfile delta base object '{baseId.Hex}' could not be read.",
                objectId: baseId.Hex)
            : (resolved.Kind, resolved.Payload);
    }

    private byte[] Inflate(long dataOffset, long size)
    {
        _pack.Position = dataOffset;
        return GitPackPayloadInflater.Inflate(_pack, size);
    }

    private byte ReadByteAt(long position)
    {
        _pack.Position = position;
        int value = _pack.ReadByte();
        if (value < 0)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile entry header runs past the end of the pack.");
        }

        return (byte)value;
    }

    private byte[] ReadExactlyAt(long position, int length)
    {
        byte[] destination = new byte[length];
        _pack.Position = position;
        _pack.ReadExactly(destination);
        return destination;
    }

    private GitPackEntryHeader ReadEntryHeader(long offset)
        => GitPackEntryHeaderParser.Read(offset, _digestLength, ReadByteAt, ReadExactlyAt);

    private static GitObjectKind ToKind(int type) => type switch
    {
        1 => GitObjectKind.Commit,
        2 => GitObjectKind.Tree,
        3 => GitObjectKind.Blob,
        4 => GitObjectKind.Tag,
        _ => throw HistoryFailures.Fail(
            HistoryDiagnosticKind.ObjectMalformed,
            $"A packfile entry declares unsupported object type {type}."),
    };
}
