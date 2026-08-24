using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// Authentication material deliberately does not live under AnalysisCacheLocation.RootPath. A
// generic CI cache restore may treat every byte below that root as untrusted; keeping an HMAC key
// there would let the same restored archive replace both an entry and the key needed to authorize
// it. The sibling authentication namespace is not part of the cache payload and must be excluded
// from CI cache paths (see the analysis-cache spec's generic-CI guidance).
internal static class AnalysisCacheHmacKeyStore
{
    private const int KeyLengthBytes = 32;
    private const string AuthenticationDirectoryName = ".archlinternet-analysis-cache-auth";
    private const string KeyFileName = "hmac-v1.key";
    private const int MaxReadRetries = 40;
    private const int RetryDelayMilliseconds = 5;
    private static readonly TimeSpan CreationMutexTimeout = TimeSpan.FromSeconds(5);

    // Test-only seam. It makes the external trust boundary assertable without placing test keys
    // in a developer's real profile. Production leaves this null.
    internal static string? TestAuthenticationParentOverride { get; set; }

    internal static string GetKeyPath(string cacheRootPath)
    {
        string root = Path.GetFullPath(cacheRootPath);
        string parent = TestAuthenticationParentOverride
            ?? Path.GetDirectoryName(root)
            ?? throw new AnalysisCacheLocationRejectedException(
                $"Cannot derive an authentication directory for cache root '{cacheRootPath}'.");
        string rootId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root)));
        return Path.Combine(Path.GetFullPath(parent), AuthenticationDirectoryName, rootId, KeyFileName);
    }

    public static byte[] GetOrCreateKey(string cacheRootPath)
    {
        string keyPath = GetKeyPath(cacheRootPath);
        string keyDirectory = Path.GetDirectoryName(keyPath)
            ?? throw new AnalysisCacheLocationRejectedException("Cache authentication directory is invalid.");
        EnsureSafeKeyDirectory(keyDirectory);
        EnsureSafeKeyPath(keyPath);

        byte[]? existing = TryReadExistingKey(keyPath);
        if (existing is not null)
        {
            return existing;
        }

        // FileMode.CreateNew makes publishing a key atomic, but it alone does not serialize
        // self-healing. Several losing callers can otherwise mistake the winner's short-lived
        // FileShare.None lock for corruption and each replace a valid key. A named mutex covers
        // both threads and local processes for this cache-root-specific key path.
        using Mutex creationMutex = new(initiallyOwned: false, GetCreationMutexName(keyPath));
        bool mutexAcquired = false;
        try
        {
            try
            {
                mutexAcquired = creationMutex.WaitOne(CreationMutexTimeout);
            }
            catch (AbandonedMutexException)
            {
                // The prior owner exited while creating or repairing the key. Re-check and
                // complete that work while holding ownership of the now-abandoned mutex.
                mutexAcquired = true;
            }

            if (!mutexAcquired)
            {
                throw new IOException("Timed out waiting to create the cache authentication key.");
            }

            EnsureSafeKeyDirectory(keyDirectory);
            EnsureSafeKeyPath(keyPath);
            existing = TryReadExistingKey(keyPath);
            if (existing is not null)
            {
                return existing;
            }

            Directory.CreateDirectory(keyDirectory);
            EnsureSafeKeyDirectory(keyDirectory);
            EnsureSafeKeyPath(keyPath);
            RestrictToOwnerOnUnix(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            return CreateExclusiveOrAdoptWinner(keyPath, attemptSelfHealOnCorruption: true);
        }
        finally
        {
            if (mutexAcquired)
            {
                creationMutex.ReleaseMutex();
            }
        }
    }

    private static string GetCreationMutexName(string keyPath) =>
        "arch-linter-net-analysis-cache-hmac-" + Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(keyPath)));

    private static void EnsureSafeKeyDirectory(string keyDirectory)
    {
        string authenticationRoot = Path.GetDirectoryName(keyDirectory)
            ?? throw new AnalysisCacheLocationRejectedException("Cache authentication directory is invalid.");

        // Do the check both before creating a missing directory and immediately before key I/O.
        // A pre-created .archlinternet-analysis-cache-auth/<root-id> link must never be read,
        // created through, or removed as part of key self-healing.
        if (FileSystemContainmentGuard.HasReparsePointAncestor(keyDirectory, authenticationRoot)
            || FileSystemContainmentGuard.IsReparsePoint(authenticationRoot)
            || FileSystemContainmentGuard.IsReparsePoint(keyDirectory))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"Refusing cache authentication path '{keyDirectory}' because it crosses a symlink/reparse point.");
        }
    }

    private static void EnsureSafeKeyPath(string keyPath)
    {
        if (FileSystemContainmentGuard.IsReparsePoint(keyPath))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"Refusing cache authentication key '{keyPath}' because it is a symlink/reparse point.");
        }
    }

    private static byte[] CreateExclusiveOrAdoptWinner(string keyPath, bool attemptSelfHealOnCorruption)
    {
        EnsureSafeKeyDirectory(Path.GetDirectoryName(keyPath)!);
        EnsureSafeKeyPath(keyPath);
        byte[] newKey = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        try
        {
            using (FileStream stream = new(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(newKey, 0, newKey.Length);
                stream.Flush(flushToDisk: true);
            }

            RestrictToOwnerOnUnix(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return newKey;
        }
        catch (IOException)
        {
            for (int attempt = 0; attempt < MaxReadRetries; attempt++)
            {
                EnsureSafeKeyDirectory(Path.GetDirectoryName(keyPath)!);
                EnsureSafeKeyPath(keyPath);
                byte[]? winner = TryReadExistingKey(keyPath);
                if (winner is not null)
                {
                    return winner;
                }

                Thread.Sleep(RetryDelayMilliseconds);
            }

            if (!attemptSelfHealOnCorruption)
            {
                throw;
            }

            EnsureSafeKeyDirectory(Path.GetDirectoryName(keyPath)!);
            EnsureSafeKeyPath(keyPath);
            TryDelete(keyPath);
            return CreateExclusiveOrAdoptWinner(keyPath, attemptSelfHealOnCorruption: false);
        }
    }

    private static byte[]? TryReadExistingKey(string keyPath)
    {
        if (!File.Exists(keyPath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(keyPath);
            return bytes.Length == KeyLengthBytes ? bytes : null;
        }
        catch (IOException)
        {
            // A concurrent first-use winner writes with FileShare.None. Treat that short-lived
            // lock as "not readable yet" so CreateExclusiveOrAdoptWinner's bounded retry loop
            // adopts the completed key instead of surfacing an optional-cache failure.
            return null;
        }
    }

    private static void RestrictToOwnerOnUnix(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch (IOException)
        {
            // Permission tightening is best effort; the authentication-path containment check is
            // still mandatory and any later unreadable key becomes a typed cache reject.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup: a locked or already-removed key file is not this call's problem.
        }
    }
}
