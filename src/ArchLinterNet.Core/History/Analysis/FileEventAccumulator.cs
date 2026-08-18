namespace ArchLinterNet.Core.History.Analysis;

// Collects every raw delta entry that maps to one logical file inside one commit. Several entries
// still produce exactly one canonical event, which is what makes `commit_count` commit-distinct
// rather than entry-distinct.
internal sealed class FileEventAccumulator
{
    private bool _hasRename;
    private bool _hasOrdinary;
    private bool _hasBinary;
    private FileEventKind _ordinaryKind = FileEventKind.Modify;
    private long _additions;
    private long _deletions;
    private string? _oldPath;
    private string? _newPath;

    public void MarkRename(string sourcePath, string destinationPath)
    {
        _hasRename = true;
        _oldPath = sourcePath;
        _newPath = destinationPath;
    }

    public void AddOrdinary(FileEventKind kind, LineCountStatus status, long additions, long deletions)
    {
        _ordinaryKind = _hasOrdinary ? FileEventKind.Modify : kind;
        _hasOrdinary = true;
        _hasBinary |= status == LineCountStatus.BinaryOrUnavailable;
        _additions += additions;
        _deletions += deletions;
    }

    public FileEvent ToEvent(string commitId)
    {
        if (_hasRename && !_hasOrdinary)
        {
            return new FileEvent(commitId, FileEventKind.Rename, LineCountStatus.ExactRename, 0, 0, _oldPath, _newPath);
        }

        LineCountStatus status = _hasBinary ? LineCountStatus.BinaryOrUnavailable : LineCountStatus.Text;
        FileEventKind kind = _hasRename ? FileEventKind.Rename : _ordinaryKind;
        return new FileEvent(commitId, kind, status, _additions, _deletions, _oldPath, _newPath);
    }
}
