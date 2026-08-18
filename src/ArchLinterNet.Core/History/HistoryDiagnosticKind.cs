namespace ArchLinterNet.Core.History;

// Stable fail-closed failure kinds. These are the error surface described by
// release-architecture-forensics: they are never records inside a successful ingestion result.
internal enum HistoryDiagnosticKind
{
    RepositoryNotFound,
    UnsupportedObjectFormat,
    RefUnresolved,
    RefAmbiguous,
    RefCycle,
    RefNotACommit,
    ObjectMissing,
    ObjectMalformed,
    CommitMetadataMalformed,
    AuthorEncodingInvalid,
    MessageEncodingInvalid,
    PathEncodingInvalid,
    TaskKeyOverlap,
}
