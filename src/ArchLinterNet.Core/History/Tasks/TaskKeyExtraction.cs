using System.Text;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Tasks.Abstractions;

namespace ArchLinterNet.Core.History.Tasks;

// Runs the effective extractor set over one commit's raw message payload and produces canonical
// provenance. Ordering and deduplication happen here rather than in an extractor, so registration
// order cannot leak into evidence.
internal sealed class TaskKeyExtraction(IReadOnlyList<ITaskKeyExtractor> extractors)
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static TaskKeyExtraction Default { get; } = new([new IssueTaskKeyExtractor()]);

    public (IReadOnlyList<TaskKeyMatch> Matches, IReadOnlyList<TaskKey> Keys) Extract(byte[] rawMessage, string commitId)
    {
        RequireStrictUtf8(rawMessage, commitId);
        List<TaskKeyMatch> collected = [];
        foreach (ITaskKeyExtractor extractor in extractors)
        {
            extractor.Extract(rawMessage, collected);
        }

        List<TaskKeyMatch> ordered = collected
            .Distinct()
            .OrderBy(static match => match.SpanStart)
            .ThenBy(static match => match.SpanEnd)
            .ThenBy(static match => match.ExtractorId, GitScalarValueComparer.Instance)
            .ThenBy(static match => match.Key)
            .ToList();

        RequireNoConflictingOverlap(ordered, commitId);
        IReadOnlyList<TaskKey> keys = ordered
            .Select(static match => match.Key)
            .Distinct()
            .OrderBy(static key => key)
            .ToList();

        return (ordered, keys);
    }

    // Overlapping spans that agree on the canonical key are the same evidence spelled twice; only a
    // disagreement is ambiguous, and it fails closed rather than depending on extractor precedence.
    private static void RequireNoConflictingOverlap(List<TaskKeyMatch> ordered, string commitId)
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            for (int other = index + 1; other < ordered.Count; other++)
            {
                if (ordered[other].SpanStart >= ordered[index].SpanEnd)
                {
                    break;
                }

                if (ordered[index].OverlapsWith(ordered[other]) && !ordered[index].Key.Equals(ordered[other].Key))
                {
                    throw HistoryFailures.Fail(
                        HistoryDiagnosticKind.TaskKeyOverlap,
                        $"Commit '{commitId}' has overlapping task-reference matches that map to different canonical task keys.",
                        objectId: commitId,
                        spanStart: ordered[other].SpanStart,
                        spanEnd: ordered[index].SpanEnd);
                }
            }
        }
    }

    private static void RequireStrictUtf8(byte[] rawMessage, string commitId)
    {
        try
        {
            _strictUtf8.GetString(rawMessage);
        }
        catch (DecoderFallbackException)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.MessageEncodingInvalid,
                $"The raw message payload of commit '{commitId}' is not valid UTF-8.",
                objectId: commitId);
        }
    }
}
