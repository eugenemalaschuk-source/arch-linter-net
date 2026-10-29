namespace ArchLinterNet.Core.History;

// A fail-closed diagnostic carries a stable kind plus whatever canonical identity was available at
// the failure point. Every optional field stays null rather than being filled with a placeholder,
// so a consumer can tell "no object identity was known" from "this object failed".
internal sealed class HistoryDiagnostic(
    HistoryDiagnosticKind kind,
    string message,
    HistoryDiagnosticSubject? subject = null,
    HistoryDiagnosticPublication? publication = null)
{
    public HistoryDiagnosticKind Kind { get; } = kind;

    public string Message { get; } = message;

    public string? ObjectId { get; } = subject?.ObjectId;

    public string? Path { get; } = subject?.Path;

    public int? SpanStart { get; } = subject?.SpanStart;

    public int? SpanEnd { get; } = subject?.SpanEnd;

    public string? PublicationStatus { get; } = publication?.Status;

    public bool PublicationCancelled { get; } = publication?.Cancelled ?? false;

    public IReadOnlyList<string> FailedDestinations { get; } = publication?.FailedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> CommittedDestinations { get; } = publication?.CommittedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> DeliveredDestinations { get; } = publication?.DeliveredDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> UncommittedDestinations { get; } = publication?.UncommittedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> PublicationDetails { get; } = publication?.Details ?? Array.Empty<string>();

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
        HistoryDiagnosticKind.ReportSerializationInvalid => "report_serialization_invalid",
        HistoryDiagnosticKind.ReportPublicationFailed => "report_publication_failed",
        HistoryDiagnosticKind.AnalysisCancelled => "analysis_cancelled",
        _ => "unknown",
    };
}

internal sealed record HistoryDiagnosticSubject(
    string? ObjectId = null,
    string? Path = null,
    int? SpanStart = null,
    int? SpanEnd = null);

internal sealed record HistoryDiagnosticPublication(
    string? Status = null,
    bool Cancelled = false,
    IReadOnlyList<string>? FailedDestinations = null,
    IReadOnlyList<string>? CommittedDestinations = null,
    IReadOnlyList<string>? DeliveredDestinations = null,
    IReadOnlyList<string>? UncommittedDestinations = null,
    IReadOnlyList<string>? Details = null)
{
    public IReadOnlyList<string> FailedDestinations { get; } = FailedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> CommittedDestinations { get; } = CommittedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> DeliveredDestinations { get; } = DeliveredDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> UncommittedDestinations { get; } = UncommittedDestinations ?? Array.Empty<string>();

    public IReadOnlyList<string> Details { get; } = Details ?? Array.Empty<string>();
}
