namespace ArchLinterNet.Core.IO.Abstractions;

public interface IArchitectureFileSystem
{
    /// <summary>
    /// Opens a file for general reads.
    /// </summary>
    /// <remarks>
    /// This legacy default remains for source and runtime compatibility. It materializes text and
    /// therefore must not be used for byte-sensitive evidence ingestion; that boundary requires
    /// <see cref="IArchitectureEvidenceFileSystem"/> instead.
    /// </remarks>
    Stream OpenRead(string path)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ReadAllText(path)), writable: false);
    }

    bool FileExists(string path);

    string ReadAllText(string path);

    IEnumerable<string> ReadLines(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

    DateTime GetLastWriteTimeUtc(string path);

    string GetCurrentDirectory();
}
