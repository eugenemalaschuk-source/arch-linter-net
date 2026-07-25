namespace ArchLinterNet.Core.Model;

// Seam-safe translation of ArchitecturePolicyImportException (ArchLinterNet.Core.Contracts) —
// hosts (CLI, Testing) are forbidden from depending on Core.Contracts directly, so every
// application service that loads a policy document (validation, graph, baseline) catches the
// Contracts-level exception at its own seam boundary and re-throws this instead, carrying the
// same diagnostic and an already-stringified category.
public sealed class ArchitecturePolicyLoadException : InvalidOperationException
{
    public ArchitecturePolicyLoadException(
        string message, ArchitecturePolicyDiagnostic? diagnostic, string category, Exception? innerException = null)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
        Category = category;
    }

    public ArchitecturePolicyDiagnostic? Diagnostic { get; }

    public string Category { get; }
}
