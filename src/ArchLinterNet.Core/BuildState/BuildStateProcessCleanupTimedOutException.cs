namespace ArchLinterNet.Core.BuildState;

// Thrown instead of a bare OperationCanceledException when a killed dotnet build/restore child
// process does not report exit within the bounded post-kill wait — Process.Kill is asynchronous
// and, on a hostile or kernel-stuck child, the wait can time out without the process actually
// having terminated. Deriving from OperationCanceledException keeps every existing
// OperationCanceledException catch branch (ValidateCommandHandler, ArchitectureValidationApplicationService,
// the Testing API) working unchanged; the extra fields let cancellation evidence honestly report
// that cleanup could not be confirmed within the deadline instead of implying a clean kill.
public sealed class BuildStateProcessCleanupTimedOutException : OperationCanceledException
{
    public int ProcessId { get; }
    public int TimeoutMs { get; }

    public BuildStateProcessCleanupTimedOutException(int processId, int timeoutMs, CancellationToken token)
        : base(
            $"Cancelled dotnet build/restore process {processId} did not exit within {timeoutMs}ms after " +
            "being killed; it may still be running and holding resources.",
            token)
    {
        ProcessId = processId;
        TimeoutMs = timeoutMs;
    }
}
