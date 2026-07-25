namespace ArchLinterNet.Cli.Abstractions;

internal interface IFileSystem
{
    bool FileExists(string path);

    void WriteAllText(string path, string contents);

    void WriteAllTextToTemp(string path, string contents);

    string ResolveTempPath(string path);

    void RenameTempToTarget(string tempPath, string targetPath);

    void DeleteFile(string path);

    bool CanWriteToDirectory(string path);
}
