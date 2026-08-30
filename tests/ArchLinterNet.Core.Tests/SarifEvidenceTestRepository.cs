using System.Text;

namespace ArchLinterNet.Core.Tests;

internal sealed class SarifEvidenceTestRepository : IDisposable
{
    internal SarifEvidenceTestRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), $"arch-linter-sarif-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    internal string Root { get; }

    internal string AddUtf8File(string relativePath, string content)
    {
        return AddFile(relativePath, Encoding.UTF8.GetBytes(content));
    }

    internal string AddFile(string relativePath, byte[] content)
    {
        string path = GetPath(relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, content);
        return path;
    }

    internal string GetPath(string relativePath)
    {
        return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
