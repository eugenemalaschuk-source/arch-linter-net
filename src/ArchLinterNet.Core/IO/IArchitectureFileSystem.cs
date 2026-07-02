namespace ArchLinterNet.Core.IO;

public interface IArchitectureFileSystem
{
    bool FileExists(string path);

    string ReadAllText(string path);

    IEnumerable<string> ReadLines(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    DateTime GetLastWriteTimeUtc(string path);

    string GetCurrentDirectory();
}

public sealed class ArchitectureFileSystem : IArchitectureFileSystem
{
    public static readonly ArchitectureFileSystem Real = new();

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public IEnumerable<string> ReadLines(string path)
    {
        return File.ReadLines(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return File.GetLastWriteTimeUtc(path);
    }

    public string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }
}
