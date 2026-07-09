namespace ArchLinterNet.Cli;

internal sealed class FileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents);
    }
}
