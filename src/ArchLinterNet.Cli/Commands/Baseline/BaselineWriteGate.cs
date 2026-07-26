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
    public bool TryApply(Request request, out Disposition disposition)
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
            console.Error.WriteLine(request.CommentDiagnostic);
            return false;
        }

        if (!TryConfirmOverwriteIntent(request))
        {
            return false;
        }

        // Temp-then-rename: if producing or writing the content fails, the destination still holds
        // its original bytes because nothing has been renamed over it.
        string tempPath = fileSystem.WriteAllTextToTemp(request.OutputPath, request.Yaml);
        fileSystem.RenameTempToTarget(tempPath, request.OutputPath);
        disposition = Disposition.Written;
        return true;
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

        console.Error.WriteLine(
            $"'{request.OutputPath}' already exists and {request.Command} would replace its reviewed content. " +
            "Re-run with --force to replace it, or with --dry-run to review the proposed content first.");
        return false;
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
            _ => writtenStatus,
        };
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}
