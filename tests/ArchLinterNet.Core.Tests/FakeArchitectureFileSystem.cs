using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Tests;

internal sealed class FakeArchitectureFileSystem : IArchitectureFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastWriteTimesUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private string _currentDirectory = "/fake/current";

    public void AddFile(string path, string content, DateTime lastWriteTimeUtc)
    {
        _files[path] = content;
        _lastWriteTimesUtc[path] = lastWriteTimeUtc;
    }

    public void AddDirectory(string path)
    {
        _directories.Add(path);
    }

    public void SetCurrentDirectory(string path)
    {
        _currentDirectory = path;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(path);
    }

    public string ReadAllText(string path)
    {
        return _files.TryGetValue(path, out string? content)
            ? content
            : throw new FileNotFoundException($"Fake file not found: {path}");
    }

    public IEnumerable<string> ReadLines(string path)
    {
        return ReadAllText(path).Split('\n');
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(path);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        string normalizedDirectory = path.TrimEnd('/', '\\') + "/";
        string extension = searchPattern.TrimStart('*');

        foreach (string filePath in _files.Keys)
        {
            if (!filePath.Replace('\\', '/').StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = filePath.Replace('\\', '/')[normalizedDirectory.Length..];
            if (searchOption == SearchOption.TopDirectoryOnly && relative.Contains('/'))
            {
                continue;
            }

            yield return filePath;
        }
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return _lastWriteTimesUtc.TryGetValue(path, out DateTime writeTime) ? writeTime : DateTime.MinValue;
    }

    public string GetCurrentDirectory()
    {
        return _currentDirectory;
    }
}
