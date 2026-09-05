using System.Security;
using System.Security.Cryptography;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Execution;

/// <summary>Acquires one bounded, repository-local SARIF artifact without interpreting its bytes.</summary>
internal sealed class SarifEvidenceArtifactReader
{
    private readonly IArchitectureEvidenceFileSystem _fileSystem;

    public SarifEvidenceArtifactReader(IArchitectureEvidenceFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public SarifEvidenceArtifactReadOutcome Read(
        string repositoryRoot,
        string artifactPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        PathResolution path = ResolveArtifactPath(repositoryRoot, artifactPath);
        if (!path.IsSafe)
        {
            return SarifEvidenceArtifactReadOutcome.Unsafe;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ReadBoundedBytes(path, maximumBytes, cancellationToken);
    }

    private static PathResolution ResolveArtifactPath(string repositoryRoot, string artifactPath)
    {
        if (IsUnsafeArtifactPath(artifactPath))
        {
            return PathResolution.Unsafe;
        }

        try
        {
            return ResolveContainedArtifactPath(repositoryRoot, artifactPath);
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

    private static bool IsUnsafeArtifactPath(string artifactPath)
    {
        return Path.IsPathRooted(artifactPath)
            || artifactPath.StartsWith('\\')
            || artifactPath.Contains('\0')
            || artifactPath.Contains(':');
    }

    private static PathResolution ResolveContainedArtifactPath(string repositoryRoot, string artifactPath)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string hostPath = artifactPath.Replace('\\', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(root, hostPath));
        string relativePath = Path.GetRelativePath(root, fullPath);
        if (IsOutsideRootOrUnsafeIndirection(root, fullPath, relativePath))
        {
            return PathResolution.Unsafe;
        }

        return new PathResolution(true, root, fullPath, relativePath.Replace('\\', '/'));
    }

    private static bool IsOutsideRootOrUnsafeIndirection(string root, string fullPath, string relativePath)
    {
        return Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || !FileSystemContainmentGuard.IsContained(fullPath, root)
            || FileSystemContainmentGuard.HasReparsePointAncestor(fullPath, root)
            || FileSystemContainmentGuard.IsReparsePoint(fullPath);
    }

    private SarifEvidenceArtifactReadOutcome ReadBoundedBytes(
        PathResolution path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        long bytesRead = 0;
        bool exceeded = false;
        try
        {
            if (!IsStillSafe(path))
            {
                return SarifEvidenceArtifactReadOutcome.UnsafeAfterPathResolution(path.RelativePath);
            }

            using Stream stream = _fileSystem.OpenRepositoryLocalRegularFile(path.RootPath, path.RelativePath);
            ReadIntoBuffer(stream, buffer, maximumBytes, cancellationToken, out bytesRead, out exceeded);
            return CreateReadableOutcome(path.RelativePath, buffer, exceeded, bytesRead);
        }
        catch (FileNotFoundException)
        {
            return CreateFailureOutcome(path.RelativePath, buffer, ArtifactReadFailure.Missing, bytesRead);
        }
        catch (InvalidDataException)
        {
            return CreateFailureOutcome(path.RelativePath, buffer, ArtifactReadFailure.Unsafe, bytesRead);
        }
        catch (Exception ex) when (IsUnreadableException(ex))
        {
            return CreateFailureOutcome(path.RelativePath, buffer, ArtifactReadFailure.Unreadable, bytesRead);
        }
    }

    private static void ReadIntoBuffer(
        Stream stream,
        MemoryStream buffer,
        long maximumBytes,
        CancellationToken cancellationToken,
        out long bytesRead,
        out bool exceeded)
    {
        bytesRead = 0;
        exceeded = false;
        byte[] chunk = new byte[81920];
        while (bytesRead <= maximumBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int requested = GetReadSize(maximumBytes, bytesRead, chunk.Length);
            if (requested <= 0)
            {
                return;
            }

            int count = stream.Read(chunk, 0, requested);
            if (count == 0)
            {
                return;
            }

            buffer.Write(chunk, 0, count);
            bytesRead += count;
            exceeded = bytesRead > maximumBytes;
            if (exceeded)
            {
                return;
            }
        }
    }

    private static int GetReadSize(long maximumBytes, long bytesRead, int chunkLength)
    {
        long remaining = maximumBytes == long.MaxValue ? long.MaxValue : maximumBytes - bytesRead + 1;
        return (int)Math.Min(chunkLength, remaining);
    }

    private static SarifEvidenceArtifactReadOutcome CreateReadableOutcome(
        string relativePath,
        MemoryStream buffer,
        bool exceeded,
        long bytesRead)
    {
        byte[] data = buffer.ToArray();
        return new SarifEvidenceArtifactReadOutcome(
            true,
            ArtifactReadFailure.None,
            exceeded,
            data,
            bytesRead,
            Convert.ToHexStringLower(SHA256.HashData(data)),
            relativePath);
    }

    private static SarifEvidenceArtifactReadOutcome CreateFailureOutcome(
        string relativePath,
        MemoryStream buffer,
        ArtifactReadFailure failure,
        long bytesRead)
    {
        byte[] data = buffer.ToArray();
        string? hash = data.Length == 0 ? null : Convert.ToHexStringLower(SHA256.HashData(data));
        return new SarifEvidenceArtifactReadOutcome(false, failure, false, data, bytesRead, hash, relativePath);
    }

    private static bool IsUnreadableException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or DirectoryNotFoundException
            or NotSupportedException;
    }

    private static bool IsStillSafe(PathResolution path)
    {
        try
        {
            return FileSystemContainmentGuard.IsContained(path.FullPath, path.RootPath)
                && !FileSystemContainmentGuard.HasReparsePointAncestor(path.FullPath, path.RootPath)
                && !FileSystemContainmentGuard.IsReparsePoint(path.FullPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private readonly record struct PathResolution(bool IsSafe, string RootPath, string FullPath, string RelativePath)
    {
        public static PathResolution Unsafe => new(false, string.Empty, string.Empty, string.Empty);
    }
}

internal readonly record struct SarifEvidenceArtifactReadOutcome(
    bool IsReadable,
    ArtifactReadFailure Failure,
    bool ExceededLimit,
    byte[] Data,
    long BytesRead,
    string? Sha256,
    string? RelativePath)
{
    public static SarifEvidenceArtifactReadOutcome Unsafe =>
        new(false, ArtifactReadFailure.Unsafe, false, [], 0, null, null);

    public static SarifEvidenceArtifactReadOutcome UnsafeAfterPathResolution(string relativePath) =>
        new(false, ArtifactReadFailure.Unsafe, false, [], 0, null, relativePath);
}

internal enum ArtifactReadFailure
{
    None,
    Missing,
    Unsafe,
    Unreadable,
}
