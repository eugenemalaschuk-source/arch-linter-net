namespace ArchLinterNet.Core.Contracts;

public enum ArchitecturePolicyImportErrorCategory
{
    PortablePath,
    MissingFile,
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
    public ArchitecturePolicyImportException(ArchitecturePolicyImportErrorCategory category, string message)
        : base(message)
    {
        Category = category;
    }

    public ArchitecturePolicyImportErrorCategory Category { get; }
}
