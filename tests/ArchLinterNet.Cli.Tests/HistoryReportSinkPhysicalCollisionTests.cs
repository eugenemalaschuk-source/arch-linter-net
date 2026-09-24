using System.Runtime.InteropServices;
using ArchLinterNet.Cli.Commands.History.Application;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class HistoryReportSinkPhysicalCollisionTests
{
    [TestCase(false, TestName = "HardLinkReportDestinationsAreRejected")]
    [TestCase(true, TestName = "SymbolicLinkReportDestinationsAreRejected")]
    public void PhysicalAliasesAreRejectedBeforeIngestion(bool symbolicLink)
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-history-report-alias-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string existingDestination = Path.Combine(directory, "report-a.json");
        string aliasDestination = Path.Combine(directory, "report-b.md");
        File.WriteAllText(existingDestination, "original report");

        try
        {
            CreateAliasOrIgnore(aliasDestination, existingDestination, symbolicLink);
            Assert.That(new FileSystem().AreSameExistingFile(existingDestination, aliasDestination), Is.True);

            FakeConsole console = new();
            int exitCode = new HistoryIngestCommandHandler(console, new FileSystem()).Execute(
                new HistoryIngestCommandOptions(
                    directory,
                    "HEAD",
                    "HEAD",
                    "json",
                    false,
                    ReportSinks: new[]
                    {
                        new HistoryReportSink("json", HistoryReportDestinationType.File, existingDestination),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.File, aliasDestination),
                    }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("refer to the same existing file"));
                Assert.That(File.ReadAllText(existingDestination), Is.EqualTo("original report"));
            });
        }
        finally
        {
            File.Delete(aliasDestination);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CreateAliasOrIgnore(string aliasPath, string targetPath, bool symbolicLink)
    {
        try
        {
            if (symbolicLink)
            {
                File.CreateSymbolicLink(aliasPath, targetPath);
                return;
            }

            bool created = OperatingSystem.IsWindows()
                ? CreateHardLinkWindows(aliasPath, targetPath, IntPtr.Zero)
                : CreateHardLinkUnix(targetPath, aliasPath) == 0;
            if (!created)
            {
                Assert.Ignore("The test host could not create a hard-link alias.");
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or NotSupportedException)
        {
            Assert.Ignore($"The test host could not create the requested file alias: {exception.Message}");
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLinkWindows(string linkPath, string existingPath, IntPtr securityAttributes);

    [LibraryImport("libc", EntryPoint = "link", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int CreateHardLinkUnix(string existingPath, string linkPath);
}
