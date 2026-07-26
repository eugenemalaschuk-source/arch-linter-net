namespace ArchLinterNet.Cli.Abstractions;

internal interface IFileSystem
{
    bool FileExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    string WriteAllTextToTemp(string targetPath, string contents);

    string CopyFileToTemp(string sourcePath, string targetPath)
    {
        return WriteAllTextToTemp(targetPath, ReadAllText(sourcePath));
    }

    void RenameTempToTarget(string tempPath, string targetPath);

    void DeleteFile(string path);

    bool CanWriteToDirectory(string path);
}
