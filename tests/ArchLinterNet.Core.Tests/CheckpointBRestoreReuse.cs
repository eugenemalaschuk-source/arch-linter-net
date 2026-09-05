namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Tracks synthetic fixture roots whose dependency restore completed during one composed
/// Checkpoint B scenario. This is test-harness-only orchestration: every invocation retains
/// <c>--ensure-built</c>, and only a later invocation for the same unchanged root skips its
/// redundant restore.
/// </summary>
internal sealed class CheckpointBRestoreReuse
{
    private readonly HashSet<string> _restoredRoots = new(StringComparer.Ordinal);

    internal string[] PrepareArguments(string workingDirectory, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.Contains("--ensure-built", StringComparer.Ordinal)
            || arguments.Contains("--no-restore", StringComparer.Ordinal))
        {
            return arguments.ToArray();
        }

        string root = Path.GetFullPath(workingDirectory);
        return _restoredRoots.Contains(root)
            ? [.. arguments, "--no-restore"]
            : arguments.ToArray();
    }

    internal void RecordCompletedEnsureBuilt(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int exitCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        // CLI exit 1 is a completed assessment with error-severity findings, not a restore/build
        // preparation failure. Exit 2 is invalid/runtime/unassessable and must not authorize reuse.
        bool completedAssessment = exitCode is 0 or 1;
        if (completedAssessment
            && arguments.Contains("--ensure-built", StringComparer.Ordinal)
            && !arguments.Contains("--no-restore", StringComparer.Ordinal))
        {
            _restoredRoots.Add(Path.GetFullPath(workingDirectory));
        }
    }
}
