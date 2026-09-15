using System.Text;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupOutputWriterPersistence
{
    internal static void EnsureNoConflict(string root, string relativePath, string contents)
    {
        string path = SafePath(root, relativePath);
        if (relativePath == BadgeSetupOutputWriter.ReadmeFileName || !File.Exists(path))
        {
            return;
        }

        if (File.ReadAllText(path, new UTF8Encoding(false)) != contents)
        {
            throw new IOException($"Managed output conflict at '{relativePath}'. Review or remove the existing managed file before retrying.");
        }
    }

    internal static void WriteManaged(string root, string relativePath, string contents)
    {
        string path = SafePath(root, relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null) Directory.CreateDirectory(directory);
        string temporaryRelativePath = relativePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string temporary = SafePath(root, temporaryRelativePath);
        File.WriteAllText(temporary, contents, new UTF8Encoding(false));
        path = SafePath(root, relativePath);
        File.Move(temporary, path, overwrite: true);
    }

    internal static string SafePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal))
        {
            throw new IOException("Managed output path escapes the setup directory.");
        }

        string canonicalRoot = Path.GetFullPath(root);
        string path = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath));
        if (!FileSystemContainmentGuard.IsContained(path, canonicalRoot)
            || FileSystemContainmentGuard.HasReparsePointAncestor(path, canonicalRoot)
            || FileSystemContainmentGuard.IsReparsePoint(path))
        {
            throw new IOException("Managed output path crosses a symlink, junction, or reparse point.");
        }

        return path;
    }

    internal static BadgeSetupOutputWriter.OriginalFile CaptureOriginal(string root, string relativePath)
    {
        string path = SafePath(root, relativePath);
        return new(relativePath, File.Exists(path), File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    internal static void RestoreOriginals(
        string root,
        IReadOnlyList<BadgeSetupOutputWriter.OriginalFile> originals)
    {
        foreach (BadgeSetupOutputWriter.OriginalFile original in originals.Reverse())
        {
            string path = SafePath(root, original.Path);
            if (original.Exists)
            {
                string directory = Path.GetDirectoryName(path)
                    ?? throw new IOException("Could not resolve the managed output directory.");
                byte[] contents = original.Contents
                    ?? throw new IOException("Original managed file contents are missing.");
                Directory.CreateDirectory(directory);
                path = SafePath(root, original.Path);
                File.WriteAllBytes(path, contents);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    internal static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

}
