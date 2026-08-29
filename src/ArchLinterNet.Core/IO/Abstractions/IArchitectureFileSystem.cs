namespace ArchLinterNet.Core.IO.Abstractions;

public interface IArchitectureFileSystem
{
    /// <summary>
    /// Opens a file for bounded, byte-preserving reads.
    /// </summary>
    /// <remarks>
    /// The default implementation keeps existing test and host implementations source-compatible.
    /// Implementations that can provide the original bytes should override this member; the
    /// fallback is intended only for the older text-oriented file-system seam.
    /// </remarks>
    Stream OpenRead(string path)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(ReadAllText(path)),
            writable: false);
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
