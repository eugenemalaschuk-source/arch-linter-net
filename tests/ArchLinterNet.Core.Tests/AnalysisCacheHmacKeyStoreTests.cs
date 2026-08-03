using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Finding #1: AnalysisCacheHmacKeyStore is the local, cache-root-scoped secret that makes
// AnalysisCacheContentDigest a keyed authenticity tag rather than an unkeyed hash. These tests
// cover the key-store contract directly: idempotent read-or-create, persistence across calls,
// per-root independence, and safe concurrent first use.
[TestFixture]
public sealed class AnalysisCacheHmacKeyStoreTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "arch-linter-net-hmac-key-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void GetOrCreateKey_FirstCall_PersistsA256BitKeyOutsideTheEntryTree()
    {
        byte[] key = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);

        Assert.That(key.Length, Is.EqualTo(32));
        string keyPath = Path.Combine(_root, ".keys", "hmac-v1.key");
        Assert.That(File.Exists(keyPath), Is.True);

        // Never inside the sharded *.json entry tree — a bare `Clear()`-style wipe of every *.json
        // file must never remove this by accident.
        Assert.That(Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories), Is.Empty);
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

    // Simulates the concurrent-first-use race the review named: two callers reach an empty cache
    // root at "the same time". Since GetOrCreateKey is deterministic about resolving to whatever
    // ends up on disk, sequential calls exercise the same "loser discards, re-reads winner" code
    // path deterministically — a genuinely parallel version of this is covered by real concurrent
    // production use, but is not reliably reproducible as a fast unit test.
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
        string keyDirectory = Path.Combine(_root, ".keys");
        Directory.CreateDirectory(keyDirectory);
        File.WriteAllBytes(Path.Combine(keyDirectory, "hmac-v1.key"), new byte[] { 1, 2, 3 });

        byte[] key = AnalysisCacheHmacKeyStore.GetOrCreateKey(_root);

        Assert.That(key.Length, Is.EqualTo(32));
    }
}
