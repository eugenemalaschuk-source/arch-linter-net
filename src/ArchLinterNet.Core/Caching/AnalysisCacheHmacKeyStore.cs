using System.Security.Cryptography;

namespace ArchLinterNet.Core.Caching;

// Finding #1: the local, machine/cache-root-scoped HMAC secret that makes AnalysisCacheContentDigest
// a genuine authenticity tag (HMAC-SHA256) instead of an unkeyed SHA-256 hash. An unkeyed hash only
// proves an entry's bytes weren't corrupted in transit — anyone who can write the entry file can
// also recompute a matching unkeyed digest, so it never proved ArchLinterNet itself produced the
// entry. Keying the digest with a secret that lives outside the sharded entry tree closes that gap:
// a poisoned/hand-edited entry can no longer forge a matching tag without also knowing this key.
//
// Threat model, stated plainly (do not oversell this): this defeats "attacker can freely hand-edit
// the persisted JSON entry file" — the common poisoned/restored-CI-cache scenario review finding #1
// named. It does NOT defeat "attacker with full read/write access to everything under
// AnalysisCacheLocation.RootPath's parent, including this key file" — an attacker who can also read
// or overwrite the key can forge a matching tag for content they choose. That residual is an
// accepted local-trust-boundary limit, the same as it would be for any local secret-based scheme
// protecting data at the same trust level as the secret itself; it is not a claim that this key
// resists a fully co-located attacker.
internal static class AnalysisCacheHmacKeyStore
{
    private const int KeyLengthBytes = 32;

    // Colocated under the cache root but outside the sharded entry tree — a cache "clear" wiping
    // every *.json entry (AnalysisCacheStore.Clear) never touches this directory, so the key
    // outlives any number of entry clears for the lifetime of the cache root itself.
    private const string KeyDirectoryName = ".keys";
    private const string KeyFileName = "hmac-v1.key";

    // Bounds how long a loser of the concurrent-create race below waits for the winner to finish
    // writing before concluding the file is genuinely corrupt (not merely mid-write) and self-healing.
    private const int MaxReadRetries = 40;
    private const int RetryDelayMilliseconds = 5;

    // Read-or-created idempotently: the common case is a fast existing-file read. First use at a
    // given cache root generates a new 256-bit CSPRNG key and publishes it via an exclusive,
    // atomic file creation (FileMode.CreateNew — an O_EXCL-equivalent create the OS itself
    // serializes, unlike a plain "check File.Exists then File.Move" sequence, which is not
    // guaranteed atomic across platforms and was found to lose this exact race under real
    // concurrent load). Under a concurrent first-use race (two callers reach here with no key file
    // yet at the same time), the loser's own generated key is discarded entirely and it instead
    // waits briefly to read back whatever the winner published — both callers observe the exact
    // same key either way, never two different keys silently used for different entries under the
    // same cache root.
    public static byte[] GetOrCreateKey(string cacheRootPath)
    {
        string canonicalRoot = Path.GetFullPath(cacheRootPath);
        string keyDirectory = Path.Combine(canonicalRoot, KeyDirectoryName);
        string keyPath = Path.Combine(keyDirectory, KeyFileName);

        byte[]? existing = TryReadExistingKey(keyPath);
        if (existing is not null)
        {
            return existing;
        }

        Directory.CreateDirectory(keyDirectory);
        RestrictToOwnerOnUnix(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return CreateExclusiveOrAdoptWinner(keyPath, attemptSelfHealOnCorruption: true);
    }

    private static byte[] CreateExclusiveOrAdoptWinner(string keyPath, bool attemptSelfHealOnCorruption)
    {
        byte[] newKey = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        try
        {
            using (FileStream stream = new(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(newKey, 0, newKey.Length);
                stream.Flush(flushToDisk: true);
            }

            // Windows: no custom ACL is attempted here — this relies on the platform's default
            // user-profile ACL (the cache root normally lives under %LOCALAPPDATA%, already
            // per-user by default). A half-implemented custom ACL would be worse than none; this
            // limitation is deliberate, not an oversight — see the type-level remarks above.
            RestrictToOwnerOnUnix(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return newKey;
        }
        catch (IOException)
        {
            // Lost the exclusive-create race (or found an existing, possibly still-being-written,
            // file). Wait briefly for whatever is on disk to become a valid, fully-written key
            // rather than assuming corruption immediately — the winner may still be mid-write.
            for (int attempt = 0; attempt < MaxReadRetries; attempt++)
            {
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

            // Genuinely corrupt (wrong length) or otherwise unreadable after waiting — not useful
            // to anyone, and every entry authenticated under it would already be
            // unreadable/rejected regardless. Self-heal by removing it and retrying once more
            // (recursion depth is bounded to exactly one retry via attemptSelfHealOnCorruption).
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
            // Best-effort permission tightening — a filesystem that rejects chmod (some network
            // mounts) must not turn key creation into a hard failure; the key is still usable, just
            // not maximally hardened at the filesystem-permission layer on that mount.
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
        }
    }
}
