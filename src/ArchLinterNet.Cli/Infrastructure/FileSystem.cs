using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

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

    public void WriteAllTextToTemp(string path, string contents)
    {
        string absolutePath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath + ".tmp", contents);

        File.WriteAllText(path + ".tmp", contents);
    }

    public void RenameTempToTarget(string tempPath, string targetPath)
    {
        File.Move(tempPath, targetPath, overwrite: true);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public bool CanWriteToDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        string probePath = Path.Combine(directory, ".writeprobe_" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            try { File.Delete(probePath); } catch { }
            return false;
        }
    }
}
