using System.IO;
using System.IO.Compression;

namespace ArchLinterNet.Core.History.Git;

// One `.pack`/`.idx` pair. Base objects reached through `OBJ_OFS_DELTA`/`OBJ_REF_DELTA` are cached
// by pack offset because long delta chains otherwise re-inflate the same base once per link.
internal sealed class GitPackFile : IDisposable
{
    private const int TypeCommit = 1;
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
        (int type, long size, long dataOffset, long baseOffset, GitObjectId baseId) = ReadEntryHeader(offset);
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

    private (int Type, long Size, long DataOffset, long BaseOffset, GitObjectId BaseId) ReadEntryHeader(long offset)
    {
        long position = offset;
        byte current = ReadByte(ref position);
        int type = (current >> 4) & 0x07;
        long size = current & 0x0F;
        int shift = 4;
        while ((current & 0x80) != 0)
        {
            current = ReadByte(ref position);
            size |= (long)(current & 0x7F) << shift;
            shift += 7;
        }

        long baseOffset = 0;
        GitObjectId baseId = default;
        if (type == TypeOffsetDelta)
        {
            current = ReadByte(ref position);
            long relative = current & 0x7F;
            while ((current & 0x80) != 0)
            {
                current = ReadByte(ref position);
                relative = ((relative + 1) << 7) | (uint)(current & 0x7F);
            }

            baseOffset = offset - relative;
            if (baseOffset < 0)
            {
                throw HistoryFailures.Fail(
                    HistoryDiagnosticKind.ObjectMalformed,
                    "A packfile offset delta points before the start of its pack.");
            }
        }
        else if (type == TypeReferenceDelta)
        {
            byte[] digest = new byte[_digestLength];
            ReadExactly(position, digest);
            position += _digestLength;
            baseId = GitObjectId.FromBytes(digest);
        }
        else if (type is < TypeCommit or > 4)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                $"A packfile entry declares unsupported object type {type}.");
        }

        return (type, size, position, baseOffset, baseId);
    }

    private byte[] Inflate(long dataOffset, long size)
    {
        _pack.Position = dataOffset;
        byte[] content = new byte[checked((int)size)];
        using ZLibStream stream = new(_pack, CompressionMode.Decompress, leaveOpen: true);
        stream.ReadExactly(content);
        return content;
    }

    private byte ReadByte(ref long position)
    {
        _pack.Position = position;
        int value = _pack.ReadByte();
        if (value < 0)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.ObjectMalformed,
                "A packfile entry header runs past the end of the pack.");
        }

        position++;
        return (byte)value;
    }

    private void ReadExactly(long position, byte[] destination)
    {
        _pack.Position = position;
        _pack.ReadExactly(destination);
    }

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
