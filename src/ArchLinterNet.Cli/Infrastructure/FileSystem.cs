using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class FileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents);
    }

    public string WriteAllTextToTemp(string targetPath, string contents)
    {
        string absolutePath = Path.GetFullPath(targetPath);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = absolutePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(tempPath, contents);

        long fileSize = new FileInfo(tempPath).Length;
        const long MaxReportFileSize = 100L * 1024 * 1024;
        if (fileSize > MaxReportFileSize)
        {
            try { File.Delete(tempPath); } catch { }
            throw new InvalidOperationException(
                $"Report file exceeds maximum size of {MaxReportFileSize} bytes.");
        }

        return tempPath;
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
