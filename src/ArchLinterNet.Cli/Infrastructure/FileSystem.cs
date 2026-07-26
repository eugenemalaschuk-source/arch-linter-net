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
            // The InvalidOperationException below is what the caller acts on regardless of
            // whether this best-effort cleanup of the oversized temp file succeeds.
            DeleteBestEffort(tempPath);
            throw new InvalidOperationException(
                $"Report file exceeds maximum size of {MaxReportFileSize} bytes.");
        }

        return tempPath;
    }

    public string CopyFileToTemp(string sourcePath, string targetPath)
    {
        string absolutePath = Path.GetFullPath(targetPath);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = absolutePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.Copy(sourcePath, tempPath);
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
            // The false return already communicates the write failure regardless of whether this
            // best-effort cleanup of the probe file succeeds.
            DeleteBestEffort(probePath);
            return false;
        }
    }

    private static void DeleteBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Deliberately swallowed — this is a best-effort cleanup path, not the operation the
            // caller cares about the result of.
        }
    }
}
