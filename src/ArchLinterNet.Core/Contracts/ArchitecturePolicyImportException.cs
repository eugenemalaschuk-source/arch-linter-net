using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public enum ArchitecturePolicyImportErrorCategory
{
    PortablePath = 0,
    MissingFile = 1,
    OutOfBoundary = 2,
    PathCaseMismatch = 3,
    Cycle = 4,
    DuplicateImport = 5,
    GraphLimit = 6,
    SourceShape = 7,
    CompositionConflict = 8,
    UnreadableFile = 9
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
