using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Finding #1: AnalysisCacheHmacKeyStore is the local, cache-root-scoped secret that makes
// AnalysisCacheContentDigest a keyed authenticity tag rather than an unkeyed hash. These tests
// cover the key-store contract directly: idempotent read-or-create, persistence across calls,
// per-root independence, and safe concurrent first use.
[TestFixture]
[NonParallelizable]
public sealed class AnalysisCacheHmacKeyStoreTests
{
    private string _root = null!;
    private string _authenticationParent = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-key-tests", Guid.NewGuid().ToString("N"));
        _authenticationParent = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_authenticationParent);
        AnalysisCacheHmacKeyStore.TestAuthenticationParentOverride = _authenticationParent;
    }

    [TearDown]
    public void TearDown()
    {
        AnalysisCacheHmacKeyStore.TestAuthenticationParentOverride = null;
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_authenticationParent))
        {
            Directory.Delete(_authenticationParent, recursive: true);
        }
    }

    [Test]
    public void GetOrCreateKey_FirstCall_PersistsA256BitKeyOutsideTheCacheRoot()
    {
        byte[] key = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);

        Assert.That(key.Length, Is.EqualTo(32));
        string keyPath = AnalysisCacheHmacKeyStore.GetKeyPath(_root);
        Assert.That(File.Exists(keyPath), Is.True);
        Assert.That(keyPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.False);

        Assert.That(keyPath, Does.Contain(".archlinternet-analysis-cache-auth"));
    }

    [Test]
    public void GetOrCreateKey_CalledTwice_ReturnsTheSamePersistedKey()
    {
        byte[] first = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);
        byte[] second = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void GetOrCreateKey_TwoDifferentCacheRoots_GenerateIndependentKeys()
    {
        string otherRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-key-tests-other", Guid.NewGuid().ToString("N"));
        try
        {
            byte[] keyA = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);
            byte[] keyB = AnalysisCacheHmacKeyStore.GetOrCreateKey(otherRoot);

            Assert.That(keyA, Is.Not.EqualTo(keyB));
        }
        finally
        {
            if (Directory.Exists(otherRoot))
            {
                Directory.Delete(otherRoot, recursive: true);
            }
        }
    }

    // Regression: concurrent first use of an empty cache root must converge on one persisted key.
    // In particular, a losing caller must not mistake another caller's short-lived file lock for
    // corruption and self-heal a key that a different caller has already observed.
    [Test]
    public void GetOrCreateKey_ConcurrentFirstUse_BothCallersObserveTheSameKey()
    {
        Task<byte[]>[] tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => AnalysisCacheHmacKeyStore.GetOrCreateKey(_root)))
            .ToArray();

        Task.WaitAll(tasks);

        byte[] first = tasks[0].Result;
        Assert.That(tasks.Select(t => t.Result), Is.All.EqualTo(first));
    }

    [Test]
    public void GetOrCreateKey_CorruptExistingKeyFile_SelfHealsToANewValidKey()
    {
        string keyPath = AnalysisCacheHmacKeyStore.GetKeyPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.WriteAllBytes(keyPath, new byte[] { 1, 2, 3 });

        byte[] key = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);

        Assert.That(key.Length, Is.EqualTo(32));
    }

    [Test]
    public void GetOrCreateKey_AuthenticationDirectorySymlink_IsRejectedWithoutTouchingTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        try
        {
            string authenticationDirectory = Path.Combine(_authenticationParent, ".archlinternet-analysis-cache-auth");
            Directory.CreateSymbolicLink(authenticationDirectory, outsideTarget);

            Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheHmacKeyStore.GetOrCreateKey(_root));
            Assert.That(Directory.EnumerateFileSystemEntries(outsideTarget), Is.Empty);
        }
        finally
        {
            string authenticationDirectory = Path.Combine(_authenticationParent, ".archlinternet-analysis-cache-auth");
            if (Directory.Exists(authenticationDirectory))
            {
                new DirectoryInfo(authenticationDirectory).Delete();
            }

            if (Directory.Exists(outsideTarget))
            {
                Directory.Delete(outsideTarget, recursive: true);
            }
        }
    }

    [Test]
    public void GetOrCreateKey_KeyFileSymlink_IsRejectedWithoutTouchingTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-key-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outsideTarget, "outside key target");
        string keyPath = AnalysisCacheHmacKeyStore.GetKeyPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.CreateSymbolicLink(keyPath, outsideTarget);
        try
        {
            Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheHmacKeyStore.GetOrCreateKey(_root));
            Assert.That(File.ReadAllText(outsideTarget), Is.EqualTo("outside key target"));
        }
        finally
        {
            if (File.Exists(keyPath))
            {
                new FileInfo(keyPath).Delete();
            }

            if (File.Exists(outsideTarget))
            {
                File.Delete(outsideTarget);
            }
        }
    }
}
