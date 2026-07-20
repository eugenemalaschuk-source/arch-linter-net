using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public enum ArchitecturePolicyImportErrorCategory
{
    PortablePath,
    MissingFile,
    UnreadableFile,
    OutOfBoundary,
    PathCaseMismatch,
    Cycle,
    DuplicateImport,
    GraphLimit,
    SourceShape,
    CompositionConflict
}

public sealed class ArchitecturePolicyImportException : InvalidOperationException
{
    public ArchitecturePolicyImportException(
        ArchitecturePolicyImportErrorCategory category,
        string message)
        : this(category, message, diagnostic: null)
    {
    }

    public ArchitecturePolicyImportException(
        ArchitecturePolicyImportErrorCategory category,
        string message,
        ArchitecturePolicyDiagnostic? diagnostic)
        : base(message)
    {
        Category = category;
        Diagnostic = diagnostic;
    }

    public ArchitecturePolicyImportErrorCategory Category { get; }

    public ArchitecturePolicyDiagnostic? Diagnostic { get; }
}
