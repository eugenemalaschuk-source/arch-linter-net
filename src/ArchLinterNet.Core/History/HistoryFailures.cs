using System.IO;

namespace ArchLinterNet.Core.History;

// One place that turns a fail-closed condition into the pipeline's unwinding exception, so every
// throw site stays a single readable line and every diagnostic keeps its stable kind.
internal static class HistoryFailures
{
    public static HistoryFailureException Fail(
        HistoryDiagnosticKind kind,
        string message,
        string? objectId = null,
        string? path = null,
        int? spanStart = null,
        int? spanEnd = null)
    {
        HistoryDiagnostic diagnostic = new(kind, message, objectId, path, spanStart, spanEnd);
        return new HistoryFailureException(message, diagnostic);
    }

    public static HistoryDiagnostic DiagnosticOf(HistoryFailureException exception)
        => (HistoryDiagnostic)exception.Diagnostic;

    // Loose objects and packfiles are untrusted bytes: corrupt or truncated input must fail closed
    // through a HistoryDiagnostic, not surface a raw runtime exception from zlib inflation, stream
    // reads, or checked numeric casts. A HistoryFailureException already carries its own diagnostic
    // and passes through unchanged; only the exception shapes .NET's own binary/stream readers throw
    // on malformed input are converted here.
    public static T WrapObjectAccess<T>(HistoryDiagnosticKind kind, string message, string? objectId, string? path, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (IsUntrustedDataException(exception))
        {
            throw Fail(kind, $"{message}: {exception.Message}", objectId, path);
        }
    }

    private static bool IsUntrustedDataException(Exception exception)
        => exception is EndOfStreamException or InvalidDataException or OverflowException or ArgumentOutOfRangeException or IOException;
}
