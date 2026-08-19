namespace ArchLinterNet.Core.History;

// Either a complete ingestion result or a fail-closed diagnostic — never both, and never a partially
// populated result alongside an error.
internal sealed class HistoryIngestionOutcome
{
    private HistoryIngestionOutcome(HistoryIngestionResult? result, HistoryDiagnostic? diagnostic)
    {
        Result = result;
        Diagnostic = diagnostic;
    }

    public HistoryIngestionResult? Result { get; }

    public HistoryDiagnostic? Diagnostic { get; }

    public bool Succeeded => Result is not null;

    public static HistoryIngestionOutcome Success(HistoryIngestionResult result) => new(result, null);

    public static HistoryIngestionOutcome Failure(HistoryDiagnostic diagnostic) => new(null, diagnostic);
}
