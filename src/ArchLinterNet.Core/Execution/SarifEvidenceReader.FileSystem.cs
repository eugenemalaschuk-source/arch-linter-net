using System.Security;
using System.Security.Cryptography;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Execution;

public sealed partial class SarifEvidenceReader
{
    private static PathResolution ResolveArtifactPath(string repositoryRoot, string artifactPath)
    {
        if (Path.IsPathRooted(artifactPath)
            || artifactPath.StartsWith('\\')
            || artifactPath.Contains('\0')
            || IsWindowsRootedPath(artifactPath))
        {
            return PathResolution.Unsafe;
        }

        try
        {
            string root = Path.GetFullPath(repositoryRoot);
            string hostPath = artifactPath.Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(root, hostPath));
            string relativePath = Path.GetRelativePath(root, fullPath);
            if (Path.IsPathRooted(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || !FileSystemContainmentGuard.IsContained(fullPath, root)
                || FileSystemContainmentGuard.HasReparsePointAncestor(fullPath, root)
                || FileSystemContainmentGuard.IsReparsePoint(fullPath))
            {
                return PathResolution.Unsafe;
            }

            string portable = relativePath.Replace('\\', '/');
            return new PathResolution(true, fullPath, portable);
        }
        catch (ArgumentException)
        {
            return PathResolution.Unsafe;
        }
        catch (NotSupportedException)
        {
            return PathResolution.Unsafe;
        }
        catch (IOException)
        {
            return PathResolution.Unsafe;
        }
    }

    private static bool IsWindowsRootedPath(string path)
    {
        return path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && (path[2] == '\\' || path[2] == '/');
    }

    private ByteReadOutcome ReadBoundedBytes(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        long bytesRead = 0;
        bool exceeded = false;
        try
        {
            using Stream stream = _fileSystem.OpenRead(path);
            byte[] chunk = new byte[81920];
            while (bytesRead <= maximumBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long remaining = maximumBytes == long.MaxValue
                    ? long.MaxValue
                    : maximumBytes - bytesRead + 1;
                int requested = (int)Math.Min(chunk.Length, remaining);
                if (requested <= 0)
                {
                    break;
                }

                int count = stream.Read(chunk, 0, requested);
                if (count == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, count);
                bytesRead += count;
                if (bytesRead > maximumBytes)
                {
                    exceeded = true;
                    break;
                }
            }

            byte[] data = buffer.ToArray();
            string hash = Convert.ToHexStringLower(SHA256.HashData(data));
            return new ByteReadOutcome(true, exceeded, data, bytesRead, hash);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or SecurityException
            or FileNotFoundException
            or DirectoryNotFoundException)
        {
            byte[] data = buffer.ToArray();
            string? hash = data.Length == 0 ? null : Convert.ToHexStringLower(SHA256.HashData(data));
            return new ByteReadOutcome(false, false, data, bytesRead, hash);
        }
    }

    private readonly record struct PathResolution(bool IsSafe, string FullPath, string RelativePath)
    {
        public static PathResolution Unsafe => new(false, string.Empty, string.Empty);
    }

    private readonly record struct ByteReadOutcome(
        bool IsReadable,
        bool ExceededLimit,
        byte[] Data,
        long BytesRead,
        string? Sha256);
}
