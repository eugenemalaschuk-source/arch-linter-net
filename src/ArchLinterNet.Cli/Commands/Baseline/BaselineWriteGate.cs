using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

/// <summary>
/// Everything that stands between a proposed baseline document and the file system: preview, explicit
/// overwrite intent, and an atomic write. Shared by `generate`, `update`, and `prune` so all three
/// gate a write the same way.
/// </summary>
internal sealed class BaselineWriteGate(ICliConsole console, IFileSystem fileSystem)
{
    /// <summary>How a proposed document was disposed of.</summary>
    internal enum Disposition
    {
        /// <summary>Nothing was written; the proposal went to stdout (no `--output`).</summary>
        Preview,

        /// <summary>Nothing was written; `--dry-run` was requested.</summary>
        DryRun,

        /// <summary>Nothing was written because the in-place proposal is byte-identical.</summary>
        Unchanged,

        /// <summary>The proposal was written to the destination file.</summary>
        Written,
    }

    internal sealed record Request(
        string Command,
        string? OutputPath,
        bool DryRun,
        bool Force,
        string Yaml,
        // Non-null when the source file carries comments a rewrite cannot re-anchor. A write is
        // refused; a preview or dry run still proceeds, which is what makes the refusal actionable.
        string? CommentDiagnostic,
        // Set by update/prune to the resolved `--baseline` path: naming the same file as input and
        // output is itself the statement of in-place intent, so it needs no `--force`.
        string? InPlacePath = null,
        // False under `--json`, where the proposal travels inside the single JSON document instead:
        // printing raw YAML alongside it would make stdout unparsable.
        bool EmitProposalToStdout = true);

    /// <summary>
    /// Applies the gate. Returns false when the command must fail; on success reports how the proposal
    /// was disposed of, so the caller can word its own summary.
    /// </summary>
    public bool TryApply(Request request, out Disposition disposition, CancellationToken cancellationToken = default)
    {
        disposition = Disposition.Preview;

        if (request.OutputPath == null)
        {
            // No destination named: the proposal is the output. Printed verbatim so it can be
            // reviewed or redirected, and nothing on disk is touched.
            if (request.EmitProposalToStdout)
            {
                console.Out.WriteLine(request.Yaml);
            }

            return true;
        }

        if (request.DryRun)
        {
            disposition = Disposition.DryRun;
            if (request.EmitProposalToStdout)
            {
                console.Out.WriteLine($"Dry run: '{request.OutputPath}' was not modified. Proposed content:");
                console.Out.WriteLine(request.Yaml);
            }

            return true;
        }

        if (request.CommentDiagnostic != null)
        {
            WriteError(request, "configuration-error", request.CommentDiagnostic);
            return false;
        }

        if (!TryConfirmOverwriteIntent(request))
        {
            return false;
        }

        // Temp-then-rename: if producing or writing the content fails, the destination still holds
        // its original bytes because nothing has been renamed over it.
        string tempPath = fileSystem.WriteAllTextToTemp(request.OutputPath, request.Yaml);
        RenameOrCleanUpOnCancellation(tempPath, request.OutputPath, cancellationToken);
        disposition = Disposition.Written;
        return true;
    }

    /// <summary>
    /// Copies an already-reviewed source document through the same overwrite and atomic-rename gate.
    /// Used for a no-op prune so a separate destination preserves the source's exact bytes.
    /// </summary>
    public bool TryCopySource(
        Request request, string sourcePath, out Disposition disposition, CancellationToken cancellationToken = default)
    {
        disposition = Disposition.Preview;

        if (request.OutputPath == null || request.DryRun)
        {
            return TryApply(request, out disposition, cancellationToken);
        }

        if (request.CommentDiagnostic != null)
        {
            WriteError(request, "configuration-error", request.CommentDiagnostic);
            return false;
        }

        if (!TryConfirmOverwriteIntent(request))
        {
            return false;
        }

        string tempPath = fileSystem.CopyFileToTemp(sourcePath, request.OutputPath);
        RenameOrCleanUpOnCancellation(tempPath, request.OutputPath, cancellationToken);
        disposition = Disposition.Written;
        return true;
    }

    // The staged temp file is invisible at OutputPath until this rename commits it, so a token
    // cancelled any time up to and including this check is still cancellation-safe — nothing has
    // been published yet. Re-checking only before WriteAllTextToTemp/CopyFileToTemp (the caller's
    // previous behavior) left this exact window uncovered: a signal arriving after staging but
    // before this call would still have gone on to rename and report success.
    private void RenameOrCleanUpOnCancellation(string tempPath, string outputPath, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            DeleteTempFileBestEffort(tempPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        fileSystem.RenameTempToTarget(tempPath, outputPath);
    }

    private void DeleteTempFileBestEffort(string tempPath)
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

    private bool TryConfirmOverwriteIntent(Request request)
    {
        if (request.Force || !fileSystem.FileExists(request.OutputPath!))
        {
            return true;
        }

        if (request.InPlacePath != null && SamePath(request.OutputPath!, request.InPlacePath))
        {
            return true;
        }

        WriteError(
            request,
            "output-conflict",
            $"'{request.OutputPath}' already exists and {request.Command} would replace its reviewed content. " +
            "Re-run with --force to replace it, or with --dry-run to review the proposed content first.");
        return false;
    }

    private void WriteError(Request request, string category, string message)
    {
        CliErrorOutputWriter.Write(console, request.EmitProposalToStdout ? "human" : "json", category, message);
    }

    /// <summary>
    /// The one place a disposition becomes a reported status word, so `generate`, `update`, and
    /// `prune` all say `preview`/`dry-run` for the same situations.
    /// </summary>
    public static string StatusFor(Disposition disposition, string writtenStatus)
    {
        return disposition switch
        {
            Disposition.Preview => "preview",
            Disposition.DryRun => "dry-run",
            Disposition.Unchanged => "unchanged",
            _ => writtenStatus,
        };
    }

    // Case-sensitive, because this comparison *grants* permission to overwrite: on a case-sensitive
    // filesystem `baseline.yml` and `BASELINE.yml` are two different files, and treating them as one
    // would let a run replace a file the author never named without --force. A case-variant spelling
    // of the genuinely-same file therefore asks for --force, which is the harmless direction to err in.
    // (The mirror-image check in Core's migrate — "refuse when --output *is* the source" — is
    // deliberately case-insensitive, since there fail-closed means catching more collisions.)
    private static bool SamePath(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);
    }
}
