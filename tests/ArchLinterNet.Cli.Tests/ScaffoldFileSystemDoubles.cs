using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;

namespace ArchLinterNet.Cli.Tests;

internal sealed class ScaffoldTestFileSystem(params string[] existingPaths) : IFileSystem
{
    private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);
    private readonly Dictionary<string, string> _temporaryContents = new(StringComparer.Ordinal);
    private int _noClobberMoveAttemptCount;

    public List<string> CommittedPaths { get; } = new();

    public bool RejectNextNoClobberMove { get; set; }

    public int? RejectNoClobberMoveAt { get; set; }

    public string? FailDeletePath { get; set; }

    public Action<string>? OnNoClobberMoveRejected { get; set; }

    public Dictionary<string, string> Contents { get; } = new(StringComparer.Ordinal);

    public bool FileExists(string path) => _existingPaths.Contains(path);

    public Action? OnReadAllText { get; set; }

    public string ReadAllText(string path)
    {
        string contents = Contents[path];
        OnReadAllText?.Invoke();
        return contents;
    }

    public void WriteAllText(string path, string contents)
    {
        Contents[path] = contents;
        _existingPaths.Add(path);
    }

    public string WriteAllTextToTemp(string targetPath, string contents)
    {
        string temporaryPath = targetPath + ".tmp";
        _temporaryContents[temporaryPath] = contents;
        return temporaryPath;
    }

    public void RenameTempToTarget(string tempPath, string targetPath)
    {
        Contents[targetPath] = _temporaryContents[tempPath];
        _temporaryContents.Remove(tempPath);
        _existingPaths.Add(targetPath);
        CommittedPaths.Add(targetPath);
    }

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath)
    {
        _noClobberMoveAttemptCount++;
        if (RejectNextNoClobberMove || RejectNoClobberMoveAt == _noClobberMoveAttemptCount)
        {
            RejectNextNoClobberMove = false;
            _existingPaths.Add(targetPath);
            OnNoClobberMoveRejected?.Invoke(targetPath);
            return false;
        }

        if (_existingPaths.Contains(targetPath))
        {
            return false;
        }

        RenameTempToTarget(tempPath, targetPath);
        return true;
    }

    public void DeleteFile(string path)
    {
        if (string.Equals(path, FailDeletePath, StringComparison.Ordinal))
        {
            throw new IOException($"Cannot delete {path}");
        }

        Contents.Remove(path);
        _temporaryContents.Remove(path);
        _existingPaths.Remove(path);
    }

    public bool TryCreateNewFile(string path)
    {
        if (_existingPaths.Contains(path))
        {
            return false;
        }

        Contents[path] = string.Empty;
        _existingPaths.Add(path);
        return true;
    }

    public bool DirectoryExists(string path) => true;

    public void DeleteDirectoryIfEmpty(string path) { }

    public bool CanWriteToDirectory(string path) => true;
}

internal sealed class ScaffoldLateCollisionFileSystem(int collisionAttempt) : IFileSystem
{
    private readonly FileSystem _inner = new();
    private int _noClobberMoveAttempts;

    public bool FileExists(string path) => _inner.FileExists(path);

    public string ReadAllText(string path) => _inner.ReadAllText(path);

    public void WriteAllText(string path, string contents) => _inner.WriteAllText(path, contents);

    public string WriteAllTextToTemp(string targetPath, string contents) => _inner.WriteAllTextToTemp(targetPath, contents);

    public string CopyFileToTemp(string sourcePath, string targetPath) => _inner.CopyFileToTemp(sourcePath, targetPath);

    public void RenameTempToTarget(string tempPath, string targetPath) => _inner.RenameTempToTarget(tempPath, targetPath);

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath)
    {
        _noClobberMoveAttempts++;
        if (_noClobberMoveAttempts == collisionAttempt)
        {
            _inner.WriteAllText(targetPath, "created by another process");
            return false;
        }

        return _inner.TryRenameTempToNewTarget(tempPath, targetPath);
    }

    public void DeleteFile(string path) => _inner.DeleteFile(path);

    public bool TryCreateNewFile(string path) => _inner.TryCreateNewFile(path);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public void DeleteDirectoryIfEmpty(string path) => _inner.DeleteDirectoryIfEmpty(path);

    public bool CanWriteToDirectory(string path) => _inner.CanWriteToDirectory(path);
}
