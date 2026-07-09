namespace ArchLinterNet.Cli;

internal interface IFileSystem
{
    bool FileExists(string path);

    void WriteAllText(string path, string contents);
}
