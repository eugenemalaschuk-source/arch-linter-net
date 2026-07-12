namespace ArchLinterNet.Core.IO.Abstractions;

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
