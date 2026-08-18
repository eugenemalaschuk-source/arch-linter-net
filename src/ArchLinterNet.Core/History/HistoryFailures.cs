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
}
