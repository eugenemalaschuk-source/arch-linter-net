namespace ArchLinterNet.Cli.Abstractions;

internal interface IFileSystem
{
    bool FileExists(string path);

    void WriteAllText(string path, string contents);

    void WriteAllTextToTemp(string path, string contents);

    void RenameTempToTarget(string tempPath, string targetPath);

    void DeleteFile(string path);

    bool CanWriteToDirectory(string path);
}
