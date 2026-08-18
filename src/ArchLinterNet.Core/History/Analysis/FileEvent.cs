namespace ArchLinterNet.Core.History.Analysis;

internal enum FileEventKind
{
    Add,
    Delete,
    Modify,
    Rename,
}

internal enum LineCountStatus
{
    Text,
    BinaryOrUnavailable,
    ExactRename,
}

// One canonical file event: one logical file, one canonical file-evidence commit. An accepted exact
// rename collapses its delete/add pair into a single zero-churn event; an `ambiguous_dag` candidate
// deliberately does not.
internal sealed class FileEvent(
    string commitId,
    FileEventKind kind,
    LineCountStatus lineCountStatus,
    long additions,
    long deletions,
    string? oldPath,
    string? newPath)
{
    public string CommitId { get; } = commitId;

    public FileEventKind Kind { get; } = kind;

    public LineCountStatus LineCountStatus { get; } = lineCountStatus;

    public long Additions { get; } = additions;

    public long Deletions { get; } = deletions;

    public string? OldPath { get; } = oldPath;

    public string? NewPath { get; } = newPath;

    public long Churn => Additions + Deletions;

    public string KindText => Kind switch
    {
        FileEventKind.Add => "add",
        FileEventKind.Delete => "delete",
        FileEventKind.Modify => "modify",
        FileEventKind.Rename => "rename",
        _ => "unknown",
    };

    public string LineCountStatusText => LineCountStatus switch
    {
        LineCountStatus.Text => "text",
        LineCountStatus.BinaryOrUnavailable => "binary_or_unavailable",
        LineCountStatus.ExactRename => "exact_rename",
        _ => "unknown",
    };
}
