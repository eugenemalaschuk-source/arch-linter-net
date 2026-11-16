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
        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            Encoding.ASCII.GetBytes("a\nb\na\nb\n"),
            Encoding.ASCII.GetBytes("b\na\nb\na\n"),
            out LineChurnCalculator.WorkCounts work);

        Assert.That(deletions, Is.EqualTo(1));
        Assert.That(additions, Is.EqualTo(1));
        Assert.That(work.UsedMyers, Is.True);
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
    public void ExhaustiveShortSequencesMatchIndependentLcsOracle()
    {
        string[] alphabet = ["a", "b", "c"];
        string[][] sequences = EnumerateSequences(alphabet, maximumLength: 4).ToArray();

        foreach (string[] oldLines in sequences)
        {
            foreach (string[] newLines in sequences)
            {
                (long expectedAdditions, long expectedDeletions) = ReferenceChurn(oldLines, newLines);
                (long additions, long deletions) = LineChurnCalculator.Compute(
                    EncodeLines(oldLines),
                    EncodeLines(newLines));

                if ((additions, deletions) != (expectedAdditions, expectedDeletions))
                {
                    Assert.Fail(
                        $"Churn mismatch for [{string.Join(',', oldLines)}] -> [{string.Join(',', newLines)}]: " +
                        $"expected ({expectedAdditions},{expectedDeletions}), got ({additions},{deletions}).");
                }
            }
        }
    }

    [Test]
    public void SeededRepeatedLineSequencesMatchIndependentLcsOracle()
    {
        Random random = new(1138);
        string[] alphabet = ["a", "b", "c", "d"];

        for (int sample = 0; sample < 500; sample++)
        {
            string[] oldLines = RandomLines(random, alphabet);
            string[] newLines = RandomLines(random, alphabet);
            (long expectedAdditions, long expectedDeletions) = ReferenceChurn(oldLines, newLines);
            (long additions, long deletions) = LineChurnCalculator.Compute(
                EncodeLines(oldLines),
                EncodeLines(newLines));

            Assert.That(
                (additions, deletions),
                Is.EqualTo((expectedAdditions, expectedDeletions)),
                $"Seeded sample {sample} differed for [{string.Join(',', oldLines)}] -> [{string.Join(',', newLines)}].");
        }
    }

    [Test]
    public void LargeDistantSparseEditsUseBoundedMyersWork()
    {
        const int LineCount = 10_000;
        const int EditCount = 16;
        string[] oldLines = Enumerable.Range(0, LineCount).Select(static line => $"line-{line:000000}").ToArray();
        string[] newLines = [.. oldLines];
        for (int edit = 0; edit < EditCount; edit++)
        {
            int line = (int)((long)(edit + 1) * LineCount / (EditCount + 1));
            newLines[line] = $"edited-{edit:000000}";
        }

        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            EncodeLines(oldLines),
            EncodeLines(newLines),
            out LineChurnCalculator.WorkCounts work);
        int firstEditIndex = LineCount / (EditCount + 1);
        int lastEditIndex = EditCount * LineCount / (EditCount + 1);
        int reducedLength = lastEditIndex - firstEditIndex + 1;
        long baselineCells = (long)reducedLength * reducedLength;
        long measuredWork = work.FrontierSteps + work.LineComparisons;

        Assert.Multiple(() =>
        {
            Assert.That((additions, deletions), Is.EqualTo((16L, 16L)));
            Assert.That(work.UsedMyers, Is.True);
            Assert.That(work.UsedFallback, Is.False);
            Assert.That(work.FrontierSteps, Is.EqualTo(545));
            Assert.That(work.DynamicProgrammingCells, Is.Zero);
            Assert.That(work.FrontierArrayLength, Is.EqualTo(67));
            Assert.That(measuredWork, Is.LessThan((long)(baselineCells * 0.05)));
            Assert.That(measuredWork, Is.LessThanOrEqualTo(4L * (2 * reducedLength) * 33));
        });
    }

    [Test]
    public void DistanceJustAboveTheMyersBoundUsesExactFallback()
    {
        const int LineCount = 128;
        const int EditCount = 17;
        string[] oldLines = Enumerable.Range(0, LineCount).Select(static line => $"line-{line:000000}").ToArray();
        string[] newLines = [.. oldLines];
        for (int edit = 0; edit < EditCount; edit++)
        {
            int line = (int)((long)(edit + 1) * LineCount / (EditCount + 1));
            newLines[line] = $"edited-{edit:000000}";
        }

        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            EncodeLines(oldLines),
            EncodeLines(newLines),
            out LineChurnCalculator.WorkCounts work);
        int firstEditIndex = LineCount / (EditCount + 1);
        int lastEditIndex = EditCount * LineCount / (EditCount + 1);
        int reducedLength = lastEditIndex - firstEditIndex + 1;

        Assert.Multiple(() =>
        {
            Assert.That((additions, deletions), Is.EqualTo((17L, 17L)));
            Assert.That(work.UsedMyers, Is.False);
            Assert.That(work.UsedFallback, Is.True);
            Assert.That(work.FrontierSteps, Is.EqualTo(561));
            Assert.That(work.FrontierArrayLength, Is.EqualTo(67));
            Assert.That(work.DynamicProgrammingCells, Is.EqualTo((long)reducedLength * reducedLength));
            Assert.That(work.DynamicProgrammingRowLength, Is.EqualTo(reducedLength + 1));
        });
    }

    [Test]
    public void DenseDisjointDataStopsAtMyersBoundAndUsesExactTwoRowFallback()
    {
        string[] oldLines = Enumerable.Range(0, 1_000).Select(static line => $"old-{line:0000}").ToArray();
        string[] newLines = Enumerable.Range(0, 1_000).Select(static line => $"new-{line:0000}").ToArray();

        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            EncodeLines(oldLines),
            EncodeLines(newLines),
            out LineChurnCalculator.WorkCounts work);

        Assert.Multiple(() =>
        {
            Assert.That((additions, deletions), Is.EqualTo((1_000L, 1_000L)));
            Assert.That(work.UsedMyers, Is.False);
            Assert.That(work.UsedFallback, Is.True);
            Assert.That(work.FrontierSteps, Is.EqualTo(561));
            Assert.That(work.FrontierArrayLength, Is.EqualTo(67));
            Assert.That(work.DynamicProgrammingCells, Is.EqualTo(1_000_000));
            Assert.That(work.DynamicProgrammingRowLength, Is.EqualTo(1_001));
        });
    }

    [Test]
    public void ReversedUniqueLinesUseExactFallback()
    {
        string[] oldLines = Enumerable.Range(0, 200).Select(static line => $"line-{line:0000}").ToArray();
        string[] newLines = oldLines.Reverse().ToArray();

        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            EncodeLines(oldLines),
            EncodeLines(newLines),
            out LineChurnCalculator.WorkCounts work);

        Assert.That((additions, deletions), Is.EqualTo((199L, 199L)));
        Assert.That(work.UsedFallback, Is.True);
        Assert.That(work.DynamicProgrammingCells, Is.EqualTo(40_000));
    }

    [Test]
    public void LargeLengthDifferenceSkipsFrontierAllocationAndFallsBackExactly()
    {
        string[] oldLines = Enumerable.Range(0, 100).Select(static line => $"old-{line:0000}").ToArray();
        string[] newLines = Enumerable.Range(0, 100).Select(static line => $"new-{line:0000}")
            .Concat(oldLines.Reverse())
            .ToArray();

        (long expectedAdditions, long expectedDeletions) = ReferenceChurn(oldLines, newLines);
        (long additions, long deletions) = LineChurnCalculator.ComputeWithWork(
            EncodeLines(oldLines),
            EncodeLines(newLines),
            out LineChurnCalculator.WorkCounts work);

        Assert.Multiple(() =>
        {
            Assert.That((additions, deletions), Is.EqualTo((expectedAdditions, expectedDeletions)));
            Assert.That(work.UsedFallback, Is.True);
            Assert.That(work.FrontierSteps, Is.Zero);
            Assert.That(work.FrontierArrayLength, Is.Zero);
            Assert.That(work.DynamicProgrammingCells, Is.EqualTo(20_000));
        });
    }

    [Test]
    public void NulBlobStillRoutesToBinaryEvidenceWithoutTextChurn()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.WriteBytes("binary.bin", [0x01, 0x00, 0x02]);
        string first = repository.Commit("add binary");
        repository.WriteBytes("binary.bin", [0x03, 0x00, 0x04]);
        string second = repository.Commit("change binary");

        LogicalFile file = HistoryIngestionFixture.File(
            HistoryIngestionFixture.Succeed(repository, first, second),
            "binary.bin");

        Assert.That(file.Events.Single().LineCountStatusText, Is.EqualTo("binary_or_unavailable"));
        Assert.That((file.Additions, file.Deletions, file.Churn), Is.EqualTo((0L, 0L, 0L)));
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

    private static IEnumerable<string[]> EnumerateSequences(IReadOnlyList<string> alphabet, int maximumLength)
    {
        yield return [];
        for (int length = 1; length <= maximumLength; length++)
        {
            int sequenceCount = (int)Math.Pow(alphabet.Count, length);
            for (int encoded = 0; encoded < sequenceCount; encoded++)
            {
                int remainder = encoded;
                string[] sequence = new string[length];
                for (int index = 0; index < length; index++)
                {
                    sequence[index] = alphabet[remainder % alphabet.Count];
                    remainder /= alphabet.Count;
                }

                yield return sequence;
            }
        }
    }

    private static string[] RandomLines(Random random, IReadOnlyList<string> alphabet)
    {
        int length = random.Next(0, 41);
        string[] lines = new string[length];
        for (int index = 0; index < lines.Length; index++)
        {
            lines[index] = alphabet[random.Next(alphabet.Count)];
        }

        return lines;
    }

    private static byte[] EncodeLines(IReadOnlyList<string> lines)
        => Encoding.UTF8.GetBytes(string.Join('\n', lines));

    private static (long Additions, long Deletions) ReferenceChurn(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        int[] previous = new int[newLines.Count + 1];
        int[] current = new int[newLines.Count + 1];
        for (int oldIndex = 1; oldIndex <= oldLines.Count; oldIndex++)
        {
            for (int newIndex = 1; newIndex <= newLines.Count; newIndex++)
            {
                current[newIndex] = oldLines[oldIndex - 1] == newLines[newIndex - 1]
                    ? previous[newIndex - 1] + 1
                    : Math.Max(previous[newIndex], current[newIndex - 1]);
            }

            (previous, current) = (current, previous);
        }

        int lcsLength = previous[newLines.Count];
        return (newLines.Count - lcsLength, oldLines.Count - lcsLength);
    }
}
