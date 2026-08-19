namespace ArchLinterNet.Core.History;

// Fail-closed unwinding inside the ingestion pipeline. It never escapes the pipeline: the entry
// point converts it back into a HistoryDiagnostic result so no caller can observe a partially built
// ingestion result. Diagnostic is deliberately untyped here because an exception type carries no
// first-party dependency, matching ArchitecturePolicyValidationException.
internal sealed class HistoryFailureException(string message, object diagnostic)
    : InvalidOperationException(message)
{
    public object Diagnostic { get; } = diagnostic;
}
