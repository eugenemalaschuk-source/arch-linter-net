using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceTestDirectoryCleanupTests
{
    [Test]
    public void Delete_RemovesNestedDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "MyApp.Domain"));
        try
        {
            File.WriteAllText(Path.Combine(root, "MyApp.Domain", "fixture.txt"), "fixture");

            FrameworkReferenceTestDirectoryCleanup.Delete(root);

            Assert.That(Directory.Exists(root), Is.False);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void Delete_MissingDirectory_IsAlreadyClean()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-cleanup-{Guid.NewGuid():N}");

        Assert.DoesNotThrow(() => FrameworkReferenceTestDirectoryCleanup.Delete(root));
    }

    [TestCase(32)]
    [TestCase(33)]
    public void DeleteWithRetry_TransientWindowsLock_RetriesUntilSuccessful(int errorCode)
    {
        int attempts = 0;
        var delays = new List<TimeSpan>();
        var failure = WindowsIOException(errorCode);

        FrameworkReferenceTestDirectoryCleanup.DeleteWithRetry(
            () =>
            {
                if (++attempts < 3)
                {
                    throw failure;
                }
            },
            delays.Add,
            isWindows: true);

        Assert.Multiple(() =>
        {
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(delays, Has.Count.EqualTo(2));
            Assert.That(delays, Is.All.EqualTo(TimeSpan.FromMilliseconds(100)));
        });
    }

    [Test]
    public void DeleteWithRetry_PersistentWindowsLock_PropagatesOriginalErrorAfterBoundedRetries()
    {
        int attempts = 0;
        var delays = new List<TimeSpan>();
        var failure = WindowsIOException(32);

        IOException? observed = Assert.Throws<IOException>(() =>
            FrameworkReferenceTestDirectoryCleanup.DeleteWithRetry(
                () =>
                {
                    attempts++;
                    throw failure;
                },
                delays.Add,
                isWindows: true));

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.SameAs(failure));
            Assert.That(attempts, Is.EqualTo(50));
            Assert.That(delays, Has.Count.EqualTo(49));
        });
    }

    [Test]
    public void DeleteWithRetry_LockReleasedOnFinalAttempt_Succeeds()
    {
        int attempts = 0;
        int waits = 0;
        var failure = WindowsIOException(32);

        FrameworkReferenceTestDirectoryCleanup.DeleteWithRetry(
            () =>
            {
                if (++attempts < 50)
                {
                    throw failure;
                }
            },
            _ => waits++,
            isWindows: true);

        Assert.Multiple(() =>
        {
            Assert.That(attempts, Is.EqualTo(50));
            Assert.That(waits, Is.EqualTo(49));
        });
    }

    [TestCase(3)]
    [TestCase(5)]
    [TestCase(145)]
    public void DeleteWithRetry_OtherIOError_IsNotRetried(int errorCode)
    {
        var failure = WindowsIOException(errorCode);
        AssertImmediateFailure(failure, isWindows: true);
    }

    [Test]
    public void DeleteWithRetry_UnauthorizedAccess_IsNotRetried()
    {
        AssertImmediateFailure(new UnauthorizedAccessException("fixture permission failure"), isWindows: true);
    }

    [Test]
    public void DeleteWithRetry_NonWindowsSharingError_IsNotRetried()
    {
        AssertImmediateFailure(WindowsIOException(32), isWindows: false);
    }

    private static IOException WindowsIOException(int errorCode) =>
        new("fixture directory is in use", unchecked((int)(0x80070000u | (uint)errorCode)));

    private static void AssertImmediateFailure(Exception failure, bool isWindows)
    {
        int attempts = 0;
        int waits = 0;

        Exception? observed = Assert.Catch(() =>
            FrameworkReferenceTestDirectoryCleanup.DeleteWithRetry(
                () =>
                {
                    attempts++;
                    throw failure;
                },
                _ => waits++,
                isWindows));

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.SameAs(failure));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(waits, Is.Zero);
        });
    }
}
