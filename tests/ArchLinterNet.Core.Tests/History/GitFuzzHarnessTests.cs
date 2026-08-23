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
    public void OffsetDeltaSeedRunsInBothDigestModes()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-ofs-{Guid.NewGuid():N}");
        try
        {
            string inputPath = FuzzCorpus.Materialize(outputDirectory)
                .Single(path => path.EndsWith("ofs-delta-copy-base.bin", StringComparison.Ordinal));

            FuzzExecutionResult result = FuzzInputProcessor.Execute(File.ReadAllBytes(inputPath));

            Assert.That(result.Outcome, Is.EqualTo(FuzzExecutionOutcome.Canonical));
            Assert.That(result.CanonicalDigestRuns, Is.EqualTo(2));
            Assert.That(result.FailClosedDigestRuns, Is.Zero);
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
    public void UnsupportedRouteFailsClosed()
    {
        FuzzExecutionResult result = FuzzInputProcessor.Execute([0xFF]);

        Assert.That(result.Outcome, Is.EqualTo(FuzzExecutionOutcome.FailClosed));
        Assert.That(result.CanonicalDigestRuns, Is.Zero);
        Assert.That(result.FailClosedDigestRuns, Is.EqualTo(1));
    }

    [Test]
    public void BoundedReplayUsesTheRequiredWatchdogAndMemoryEnvelope()
    {
        BoundedReplayRunner.ReplayCommand command = BoundedReplayRunner.CreateCommand("synthetic.bin");

        Assert.That(BoundedReplayRunner.PerCaseTimeoutMilliseconds, Is.EqualTo(100));
        Assert.That(BoundedReplayRunner.WorkerStartupTimeoutMilliseconds, Is.EqualTo(5_000));
        Assert.That(BoundedReplayRunner.ProcessMemoryLimitBytes, Is.EqualTo(512L * 1024 * 1024));
        Assert.That(BoundedReplayRunner.WorkerReadyMarker, Does.Contain("READY"));
        Assert.That(BoundedReplayRunner.WorkerWarmupMarker, Does.Contain("WARMUP"));
        Assert.That(BoundedReplayRunner.WorkerCaseReadyMarker, Does.Contain("CASE_READY"));
        Assert.That(BoundedReplayRunner.WorkerStartMarker, Does.Contain("GO"));
        Assert.That(command.Arguments, Does.Contain("--replay-worker"));
        Assert.That(command.Arguments, Does.Contain(Path.GetFullPath("synthetic.bin")));
        Assert.That(
            command.UsesWindowsJobObject,
            Is.EqualTo(OperatingSystem.IsWindows()),
            "Windows uses a process-memory Job Object; Unix uses the prlimit launcher.");
        if (!OperatingSystem.IsWindows())
        {
            Assert.That(command.FileName, Is.EqualTo("prlimit"));
            Assert.That(command.Arguments, Does.Contain("--as=536870912"));
        }
    }
}
