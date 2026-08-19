namespace ArchLinterNet.Core.History.Tasks.Abstractions;

// The producer seam #237 supplies configured extractors through. An extractor sees the raw message
// bytes and emits provenance-carrying matches; it never decides ordering, deduplication, or overlap
// resolution, which is what keeps canonical output independent of extractor registration order.
internal interface ITaskKeyExtractor
{
    // Stable extractor ID matching `[a-z][a-z0-9._-]*`.
    string ExtractorId { get; }

    void Extract(byte[] rawMessage, ICollection<TaskKeyMatch> matches);
}
