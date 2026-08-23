using ArchLinterNet.GitFuzz;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class GitFuzzHarnessTests
{
    [Test]
    public void MaterializedSyntheticCorpusReplaysWithoutUnexpectedParserExceptions()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-corpus-{Guid.NewGuid():N}");
        try
        {
            IReadOnlyList<string> inputs = FuzzCorpus.Materialize(outputDirectory);

            Assert.That(inputs, Has.Count.GreaterThanOrEqualTo(7));
            foreach (string input in inputs)
            {
                FuzzExecutionResult result = FuzzInputProcessor.Execute(File.ReadAllBytes(input));
                Assert.That(result.Outcome, Is.Not.EqualTo(FuzzExecutionOutcome.Oversized), input);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void OversizedInputIsRejectedBeforeParserDispatch()
    {
        byte[] input = new byte[FuzzInputProcessor.MaxInputBytes + 1];

        FuzzExecutionResult result = FuzzInputProcessor.Execute(input);

        Assert.That(result.Outcome, Is.EqualTo(FuzzExecutionOutcome.Oversized));
        Assert.That(result.CanonicalDigestRuns, Is.Zero);
        Assert.That(result.FailClosedDigestRuns, Is.Zero);
    }

    [Test]
    public void UnsupportedRouteFailsClosed()
    {
        FuzzExecutionResult result = FuzzInputProcessor.Execute([0xFF]);

        Assert.That(result.Outcome, Is.EqualTo(FuzzExecutionOutcome.FailClosed));
        Assert.That(result.CanonicalDigestRuns, Is.Zero);
        Assert.That(result.FailClosedDigestRuns, Is.EqualTo(1));
    }
}
