using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi.Application;

// Shared temp-write-then-rename publish step for capture/update/migrate. The token is re-checked
// immediately before the rename that actually commits the snapshot — not just before this method is
// called — because Core returning a successful outcome and this handler starting its own I/O are
// two different moments; cancellation observed in between must still stop the commit and clean up
// the staged temp file rather than silently publishing a post-cancellation snapshot.
internal static class PublicApiTwoPhaseWriter
{
    public static void WriteAndCommit(
        IFileSystem fileSystem, string destination, string content, CancellationToken cancellationToken)
    {
        string tempPath = fileSystem.WriteAllTextToTemp(destination, content);

        if (cancellationToken.IsCancellationRequested)
        {
            DeleteTempFileBestEffort(fileSystem, tempPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        fileSystem.RenameTempToTarget(tempPath, destination);
    }

    private static void DeleteTempFileBestEffort(IFileSystem fileSystem, string tempPath)
    {
        try
        {
            fileSystem.DeleteFile(tempPath);
        }
        catch
        {
            // Cleanup only — a failure here just leaves a stray .tmp file behind, which doesn't
            // change the cancellation this call is already about to report.
        }
    }
}
