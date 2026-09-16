namespace ArchLinterNet.Core.Tests;

// A completed design-time build can leave a brief Windows sharing violation while handles
// are being released. Retry only that cleanup operation, never the test or its assertions.
internal static class FrameworkReferenceTestDirectoryCleanup
{
    internal const int MaximumAttempts = 50;
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    internal static void Delete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        DeleteWithRetry(
            () => Directory.Delete(path, recursive: true),
            Thread.Sleep,
            OperatingSystem.IsWindows());
    }

    // Delegates make both recovery and exhausted retries deterministic to test on every OS.
    internal static void DeleteWithRetry(Action delete, Action<TimeSpan> delay, bool isWindows)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                delete();
                return;
            }
            catch (IOException error) when (
                isWindows
                && attempt < MaximumAttempts
                && error.HResult is SharingViolation or LockViolation)
            {
                delay(RetryDelay);
            }
        }
    }
}
