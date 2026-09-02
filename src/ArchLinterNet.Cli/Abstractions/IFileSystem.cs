namespace ArchLinterNet.Cli.Abstractions;

internal interface IFileSystem
{
    bool FileExists(string path);

    /// <summary>
    /// Returns whether two existing paths name the same physical file. Implementations MUST follow
    /// symbolic links and recognize hard links; callers use this fail-closed guard before replacing
    /// a trusted input through a differently-spelled output path.
    /// </summary>
    bool AreSameExistingFile(string firstPath, string secondPath)
    {
        return string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    string WriteAllTextToTemp(string targetPath, string contents);

    string CopyFileToTemp(string sourcePath, string targetPath)
    {
        return WriteAllTextToTemp(targetPath, ReadAllText(sourcePath));
    }

    void RenameTempToTarget(string tempPath, string targetPath);

    /// <summary>Atomically finalizes a temporary file only when the target does not yet exist.</summary>
    /// <returns><see langword="true"/> when the temporary file was moved; otherwise, <see langword="false"/>.</returns>
    bool TryRenameTempToNewTarget(string tempPath, string targetPath);

    void DeleteFile(string path);

    /// <summary>Atomically creates a new empty file and returns <see langword="false"/> when it already exists.</summary>
    bool TryCreateNewFile(string path);

    bool DirectoryExists(string path);

    void DeleteDirectoryIfEmpty(string path);

    bool CanWriteToDirectory(string path);
}
