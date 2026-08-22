using System.Text;
using ArchLinterNet.Core.History.Evidence;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryLineChurnTests
{
    [Test]
    public void LfTerminatesLinesAndATrailingLfAddsNoExtraLine()
    {
        Assert.That(LineChurnCalculator.SplitLines("a\nb\n"u8.ToArray()).Count, Is.EqualTo(2));
        Assert.That(LineChurnCalculator.SplitLines("a\nb"u8.ToArray()).Count, Is.EqualTo(2));
        Assert.That(LineChurnCalculator.SplitLines([]).Count, Is.Zero);
        Assert.That(LineChurnCalculator.SplitLines("\n"u8.ToArray()).Count, Is.EqualTo(1));
    }

    [Test]
    public void CarriageReturnStaysPartOfTheLinePayload()
    {
        (long additions, long deletions) = LineChurnCalculator.Compute("a\n"u8.ToArray(), "a\r\n"u8.ToArray());

        Assert.That(additions, Is.EqualTo(1));
        Assert.That(deletions, Is.EqualTo(1));
    }

    // Several equally valid diff scripts exist here; only the LCS length may decide the totals.
    [Test]
    public void AmbiguousDiffScriptsProduceOneDeterministicTotal()
    {
        (long additions, long deletions) = LineChurnCalculator.Compute(
            Encoding.ASCII.GetBytes("a\nb\na\nb\n"),
            Encoding.ASCII.GetBytes("b\na\nb\na\n"));

        Assert.That(deletions, Is.EqualTo(1));
        Assert.That(additions, Is.EqualTo(1));
    }

    [Test]
    public void CommonPrefixAndSuffixReductionPreservesLcsLength()
    {
        (long additions, long deletions) = LineChurnCalculator.Compute(
            Encoding.ASCII.GetBytes("h1\nh2\nx\nt1\nt2\n"),
            Encoding.ASCII.GetBytes("h1\nh2\ny\nz\nt1\nt2\n"));

        Assert.That(deletions, Is.EqualTo(1));
        Assert.That(additions, Is.EqualTo(2));
    }

    [Test]
    public void AnEmptySideIsTheEmptyByteSequence()
    {
        Assert.That(LineChurnCalculator.Compute([], Encoding.ASCII.GetBytes("a\nb\nc\n")), Is.EqualTo((3L, 0L)));
        Assert.That(LineChurnCalculator.Compute(Encoding.ASCII.GetBytes("a\nb\nc\n"), []), Is.EqualTo((0L, 3L)));
    }

    [Test]
    public void NulDetectionIsExactByteDetection()
    {
        Assert.That(LineChurnCalculator.ContainsNul([0x41, 0x00, 0x42]), Is.True);
        Assert.That(LineChurnCalculator.ContainsNul([0x41, 0x42]), Is.False);
        Assert.That(LineChurnCalculator.ContainsNul([]), Is.False);
    }

    [Test]
    public void ChurnMatchesGitNumstatOnAMixedEdit()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\ntwo\nthree\nfour\nfive\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo prime\nthree\ninserted\nfour\nfive\n");
        string second = repository.Commit("second");

        LogicalFile file = HistoryIngestionFixture.File(HistoryIngestionFixture.Succeed(repository, first, second), "a.txt");
        string[] numstat = repository.Git("diff", "--numstat", first, second).Trim().Split('\t');

        Assert.That(file.Additions, Is.EqualTo(long.Parse(numstat[0])));
        Assert.That(file.Deletions, Is.EqualTo(long.Parse(numstat[1])));
    }
}
