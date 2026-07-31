using System.Runtime.InteropServices;

namespace ArchLinterNet.Cli.Infrastructure;

// Owns every OS-level resource behind the CLI's cancellation token: the CancellationTokenSource
// itself, the Console.CancelKeyPress subscription (kept as a named field so it can be
// unsubscribed — an anonymous delegate could never be removed), and the PosixSignalRegistration
// (disposed deterministically here rather than left to finalization). Repeated
// construction/disposal (e.g. once per test that composes a CliHost) must not accumulate
// process-wide handlers — Dispose() is idempotent and undoes exactly what the constructor did.
internal sealed class CliProcessInterruptionSource : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConsoleCancelEventHandler _cancelKeyPressHandler;
    private readonly PosixSignalRegistration? _posixSignalRegistration;
    private bool _disposed;

    // Ctrl+C/Ctrl+Break (all platforms, via Console.CancelKeyPress) and SIGTERM (Unix, e.g.
    // `timeout 30s dotnet ...`) both trigger the token. Windows has no SIGTERM equivalent to
    // register.
    public CliProcessInterruptionSource()
    {
        _cancelKeyPressHandler = OnCancelKeyPress;
        Console.CancelKeyPress += _cancelKeyPressHandler;

        _posixSignalRegistration = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? null
            : PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnPosixSignal);
    }

    public CancellationToken Token => _cancellationTokenSource.Token;

    // e.Cancel = true lets cooperative shutdown (dispose owned resources, report a cancelled
    // completion status) run instead of the runtime hard-killing the process.
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cancellationTokenSource.Cancel();
    }

    private void OnPosixSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _cancelKeyPressHandler;
        _posixSignalRegistration?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
