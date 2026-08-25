using System.Numerics;
using System.Text;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Tasks;
using ArchLinterNet.Core.History.Tasks.Abstractions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryTaskKeyExtractionTests
{
    private static readonly string[] _lexicalBoundaryExpectedKeys = { "issue#1", "issue#14", "issue#15", "issue#16" };
    private static readonly string[] _lexicalBoundaryExpectedMatchedText = { "#14", "#001", "#15", "#16" };
    private static readonly string[] _issueAndJiraKeys = { "issue#42", "jira#42" };

    [Test]
    public void DefaultExtractorLexicalBoundaryVectors()
    {
        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = Extract("abc#12 #12foo ##12 #12#13 (#14) #001 #0 fix #15, #16.");

        Assert.That(keys.Select(static key => key.ToString()), Is.EqualTo(_lexicalBoundaryExpectedKeys));
        Assert.That(matches.Select(static match => match.MatchedText), Is.EqualTo(_lexicalBoundaryExpectedMatchedText));
    }

    [Test]
    public void LeadingZeroesUnifyIntoOneCanonicalKey()
    {
        (_, IReadOnlyList<TaskKey> keys) = Extract("closes #001 and #1");

        Assert.That(keys.Count, Is.EqualTo(1));
        Assert.That(keys[0].IdText, Is.EqualTo("1"));
    }

    [Test]
    public void ProvenanceSpansAreRawMessageByteOffsets()
    {
        const string Message = "héllo #42";
        (IReadOnlyList<TaskKeyMatch> matches, _) = Extract(Message);

        // "héllo " is seven bytes because the accented scalar is two bytes.
        Assert.That(matches.Single().SpanStart, Is.EqualTo(7));
        Assert.That(matches.Single().SpanEnd, Is.EqualTo(10));
    }

    [Test]
    public void TaskKeyIdentifiersAreArbitraryPrecision()
    {
        (_, IReadOnlyList<TaskKey> keys) = Extract("see #123456789012345678901234567890");

        Assert.That(keys.Single().IdText, Is.EqualTo("123456789012345678901234567890"));
    }

    [Test]
    public void ExtractorRegistrationOrderDoesNotChangeCanonicalOutput()
    {
        StubExtractor jira = new("jira", "jira", 5, 0, 4);
        TaskKeyExtraction forward = new([new IssueTaskKeyExtractor(), jira]);
        TaskKeyExtraction reverse = new([jira, new IssueTaskKeyExtractor()]);
        byte[] message = Encoding.UTF8.GetBytes("JIRA fixes #9");

        (IReadOnlyList<TaskKeyMatch> forwardMatches, IReadOnlyList<TaskKey> forwardKeys) = forward.Extract(message, "c1");
        (IReadOnlyList<TaskKeyMatch> reverseMatches, IReadOnlyList<TaskKey> reverseKeys) = reverse.Extract(message, "c1");

        Assert.That(reverseMatches.Select(Describe), Is.EqualTo(forwardMatches.Select(Describe)));
        Assert.That(reverseKeys.Select(static key => key.ToString()), Is.EqualTo(forwardKeys.Select(static key => key.ToString())));
    }

    [Test]
    public void NamespacesKeepOtherwiseIdenticalIdentifiersDistinct()
    {
        TaskKeyExtraction extraction = new([new IssueTaskKeyExtractor(), new StubExtractor("jira", "jira", 42, 0, 4)]);

        (_, IReadOnlyList<TaskKey> keys) = extraction.Extract(Encoding.UTF8.GetBytes("JIRA see #42"), "c1");

        Assert.That(keys.Select(static key => key.ToString()), Is.EqualTo(_issueAndJiraKeys));
    }

    [Test]
    public void OverlappingMatchesWithDifferentKeysFailClosed()
    {
        TaskKeyExtraction extraction = new([new IssueTaskKeyExtractor(), new StubExtractor("jira", "jira", 99, 4, 7)]);

        HistoryFailureException failure = Assert.Throws<HistoryFailureException>(
            () => extraction.Extract(Encoding.UTF8.GetBytes("fix #12 now"), "c1"))!;

        Assert.That(((HistoryDiagnostic)failure.Diagnostic).KindText, Is.EqualTo("task_key_overlap"));
    }

    [Test]
    public void OverlappingMatchesAgreeingOnTheKeyAreNotAmbiguous()
    {
        TaskKeyExtraction extraction = new([new IssueTaskKeyExtractor(), new StubExtractor("mirror", "issue", 12, 4, 7)]);

        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = extraction.Extract(Encoding.UTF8.GetBytes("fix #12 now"), "c1");

        Assert.That(keys.Single().ToString(), Is.EqualTo("issue#12"));
        Assert.That(matches.Count, Is.EqualTo(2));
    }

    [Test]
    public void IdenticalProvenanceRecordsDeduplicate()
    {
        TaskKeyExtraction extraction = new([new IssueTaskKeyExtractor(), new IssueTaskKeyExtractor()]);

        (IReadOnlyList<TaskKeyMatch> matches, _) = extraction.Extract(Encoding.UTF8.GetBytes("fix #12"), "c1");

        Assert.That(matches.Count, Is.EqualTo(1));
    }

    private static (IReadOnlyList<TaskKeyMatch> Matches, IReadOnlyList<TaskKey> Keys) Extract(string message)
        => TaskKeyExtraction.Default.Extract(Encoding.UTF8.GetBytes(message), "c1");

    private static string Describe(TaskKeyMatch match)
        => $"{match.ExtractorId}:{match.Key}:{match.SpanStart}:{match.SpanEnd}";

    private sealed class StubExtractor(string extractorId, string keyNamespace, int id, int spanStart, int spanEnd) : ITaskKeyExtractor
    {
        public string ExtractorId => extractorId;

        public void Extract(byte[] rawMessage, ICollection<TaskKeyMatch> matches)
            => matches.Add(new TaskKeyMatch(
                extractorId,
                new TaskKey(keyNamespace, new BigInteger(id)),
                spanStart,
                spanEnd,
                Encoding.UTF8.GetString(rawMessage, spanStart, spanEnd - spanStart)));
    }
}
