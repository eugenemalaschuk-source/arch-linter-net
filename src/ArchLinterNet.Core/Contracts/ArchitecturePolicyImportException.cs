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
    UnreadableFile = 9,
    PlatformFailure = 10
}
