namespace ArchLinterNet.Core.History;

// A fail-closed diagnostic carries a stable kind plus whatever canonical identity was available at
// the failure point. Every optional field stays null rather than being filled with a placeholder,
// so a consumer can tell "no object identity was known" from "this object failed".
internal sealed class HistoryDiagnostic(
    HistoryDiagnosticKind kind,
    string message,
    string? objectId = null,
    string? path = null,
    int? spanStart = null,
    int? spanEnd = null)
{
    public HistoryDiagnosticKind Kind { get; } = kind;

    public string Message { get; } = message;

    public string? ObjectId { get; } = objectId;

    public string? Path { get; } = path;

    public int? SpanStart { get; } = spanStart;

    public int? SpanEnd { get; } = spanEnd;

    public string KindText => Kind switch
    {
        HistoryDiagnosticKind.RepositoryNotFound => "repository_not_found",
        HistoryDiagnosticKind.UnsupportedObjectFormat => "unsupported_object_format",
        HistoryDiagnosticKind.RefUnresolved => "ref_unresolved",
        HistoryDiagnosticKind.RefAmbiguous => "ref_ambiguous",
        HistoryDiagnosticKind.RefCycle => "ref_cycle",
        HistoryDiagnosticKind.RefNotACommit => "ref_not_a_commit",
        HistoryDiagnosticKind.ObjectMissing => "object_missing",
        HistoryDiagnosticKind.ObjectMalformed => "object_malformed",
        HistoryDiagnosticKind.CommitMetadataMalformed => "commit_metadata_malformed",
        HistoryDiagnosticKind.AuthorEncodingInvalid => "author_encoding_invalid",
        HistoryDiagnosticKind.MessageEncodingInvalid => "message_encoding_invalid",
        HistoryDiagnosticKind.PathEncodingInvalid => "path_encoding_invalid",
        HistoryDiagnosticKind.ConfigurationInvalid => "configuration_invalid",
        HistoryDiagnosticKind.TaskKeyOverlap => "task_key_overlap",
        _ => "unknown",
    };
}
