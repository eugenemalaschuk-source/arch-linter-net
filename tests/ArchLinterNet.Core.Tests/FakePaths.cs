namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Test fixtures seed <see cref="FakeArchitectureFileSystem"/> with POSIX-style paths like "/repo-a".
/// Production code resolves paths via <see cref="DirectoryInfo"/>/<see cref="Path.GetFullPath(string)"/>,
/// which on Windows re-roots a leading-slash path onto the current drive (e.g. "/repo-a" becomes
/// "C:\repo-a"), while on Linux it stays "/repo-a" unchanged. Routing fake roots through the same BCL
/// call the production code uses keeps both sides of every fake-filesystem comparison consistent on
/// any OS, instead of hardcoding a POSIX-only literal that only round-trips correctly on Linux.
/// </summary>
internal static class FakePaths
{
    public static string Root(string posixPath) =>
        new DirectoryInfo(posixPath).FullName.Replace('\\', '/');
}
