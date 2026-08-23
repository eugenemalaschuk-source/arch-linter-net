using System.ComponentModel;
using System.Diagnostics;
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
        BoundedReplayRunner.ReplayCommand dotnetCommand =
            BoundedReplayRunner.CreateCommand("synthetic.bin", "dotnet");

        Assert.That(BoundedReplayRunner.PerCaseTimeoutMilliseconds, Is.EqualTo(100));
        Assert.That(BoundedReplayRunner.WorkerStartupTimeoutMilliseconds, Is.EqualTo(20_000));
        Assert.That(BoundedReplayRunner.ProcessMemoryLimitBytes, Is.EqualTo(512L * 1024 * 1024));
        Assert.That(BoundedReplayRunner.WorkerReadyMarker, Does.Contain("READY"));
        Assert.That(BoundedReplayRunner.WorkerWarmupMarker, Does.Contain("WARMUP"));
        Assert.That(BoundedReplayRunner.WorkerCaseReadyMarker, Does.Contain("CASE_READY"));
        Assert.That(BoundedReplayRunner.WorkerStartMarker, Does.Contain("GO"));
        Assert.That(BoundedReplayRunner.ManagedHeapHardLimit, Is.EqualTo("0x20000000"));
        Assert.That(command.Arguments, Does.Contain("--replay-worker"));
        if (OperatingSystem.IsWindows())
        {
            Assert.That(command.Arguments, Does.Contain(Path.GetFullPath("synthetic.bin")));
            Assert.That(dotnetCommand.Arguments, Does.Contain(typeof(Program).Assembly.Location));
        }
        else
        {
            Assert.That(command.FileName, Is.EqualTo("docker"));
            Assert.That(command.Arguments, Does.Contain("--memory=512m"));
            Assert.That(command.Arguments, Does.Contain("--memory-swap=512m"));
            Assert.That(command.Arguments, Does.Contain("--network"));
            Assert.That(command.Arguments, Does.Contain(BoundedReplayRunner.ReplayContainerImage));
            Assert.That(command.ContainerName, Does.StartWith("arch-linter-git-fuzz-"));
            Assert.That(dotnetCommand.Arguments, Does.Contain("/harness/ArchLinterNet.GitFuzz.dll"));
        }
        Assert.That(
            command.UsesWindowsJobObject,
            Is.EqualTo(OperatingSystem.IsWindows()),
            "Windows uses a process-memory Job Object; Linux and macOS use a Docker cgroup.");
    }

    [Test]
    public void InvalidLauncherInputIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => BoundedReplayRunner.CreateCommand(string.Empty, "dotnet"));
    }

    [Test]
    public void MaterializeCommandReportsItsGeneratedInputs()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-materialize-{Guid.NewGuid():N}");
        try
        {
            Assert.That(Program.RunMain(["--materialize-corpus", outputDirectory], "dotnet"), Is.Zero);
            Assert.That(Directory.EnumerateFiles(outputDirectory, "*.bin"), Is.Not.Empty);
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
    public void ReplayReportsLauncherSetupFailureAsTypedExitCode()
    {
        if (!OperatingSystem.IsWindows() && !DockerIsUsable())
        {
            Assert.Ignore("The Unix launcher setup test requires a Docker daemon.");
        }

        Assert.That(
            Program.RunMain(["--replay", "missing.bin"], "arch-linter-git-fuzz-missing-launcher"),
            Is.EqualTo(
                OperatingSystem.IsWindows()
                    ? BoundedReplayRunner.ReplayLimitSetupExitCode
                    : BoundedReplayRunner.ReplayTimedOutExitCode));
    }

    // The production PerCaseTimeoutMilliseconds (100 ms) is proven by
    // BoundedReplayKillsAWorkerThatExceedsTheCaseWatchdog, which races a deterministic
    // synthetic delay rather than a real worker. This test instead checks that the bounded
    // launcher completes a genuine round trip (process/container spawn, handshake,
    // FuzzInputProcessor execution) at all, so it must not race real worker startup and
    // JIT/coverage-instrumentation overhead against that same tight production bound.
    private const int RelaxedPerCaseTimeoutMillisecondsForRoundTripOnly = 10_000;

    [Test, NonParallelizable]
    public void UserFacingReplayExecutesThroughTheBoundedWorker()
    {
        if (!OperatingSystem.IsWindows() && !DockerIsUsable())
        {
            Assert.Ignore("The Unix bounded replay acceptance test requires a Docker daemon.");
        }

        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-replay-{Guid.NewGuid():N}");
        try
        {
            string inputPath = FuzzCorpus.Materialize(outputDirectory)
                .Single(path => path.EndsWith("ofs-delta-copy-base.bin", StringComparison.Ordinal));
            BoundedReplayRunner.ReplayCommand command = BoundedReplayRunner.CreateCommand(inputPath, "dotnet");

            int exitCode = BoundedReplayRunner.Run(command, RelaxedPerCaseTimeoutMillisecondsForRoundTripOnly);

            Assert.That(exitCode, Is.Zero);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test, NonParallelizable]
    public void BoundedReplayFailsClosedWhenTheWorkerIsNotReady()
    {
        BoundedReplayRunner.ReplayCommand command = ShellCommand("echo NOT_READY");

        Assert.That(BoundedReplayRunner.Run(command), Is.EqualTo(BoundedReplayRunner.ReplayTimedOutExitCode));
    }

    [Test, NonParallelizable]
    public void BoundedReplayFailsClosedWhenWarmupDoesNotComplete()
    {
        BoundedReplayRunner.ReplayCommand command = ShellCommand("echo READY");

        Assert.That(BoundedReplayRunner.Run(command), Is.EqualTo(BoundedReplayRunner.ReplayTimedOutExitCode));
    }

    [Test, NonParallelizable]
    public void BoundedReplayKillsAWorkerThatExceedsTheCaseWatchdog()
    {
        string script = OperatingSystem.IsWindows()
            ? "echo ARCHLINTERNET_GIT_FUZZ_REPLAY_READY&echo ARCHLINTERNET_GIT_FUZZ_REPLAY_CASE_READY&ping 127.0.0.1 -n 3 >NUL"
            : "printf 'ARCHLINTERNET_GIT_FUZZ_REPLAY_READY\\nARCHLINTERNET_GIT_FUZZ_REPLAY_CASE_READY\\n'; sleep 1";
        BoundedReplayRunner.ReplayCommand command = ShellCommand(script);

        Assert.That(BoundedReplayRunner.Run(command), Is.EqualTo(BoundedReplayRunner.ReplayTimedOutExitCode));
    }

    [Test]
    public void BoundedReplayReportsAnUnavailableLauncher()
    {
        BoundedReplayRunner.ReplayCommand command = new(
            "arch-linter-git-fuzz-missing-launcher",
            [],
            UsesWindowsJobObject: false);

        Assert.Throws<InvalidOperationException>(() => BoundedReplayRunner.Run(command));
    }

    [Test, NonParallelizable]
    public void ReplayWorkerRequiresTheLauncherEnvironmentToken()
    {
        string? previous = Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, null);

            Assert.That(Program.RunMain(["--replay-worker", "missing.bin"], "dotnet"), Is.EqualTo(2));
        }
        finally
        {
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, previous);
        }
    }

    [Test, NonParallelizable]
    public void ReplayWorkerRejectsAnIncorrectWarmupMarker()
    {
        string? previous = Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable);
        TextReader originalInput = Console.In;
        try
        {
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, "1");
            Console.SetIn(new StringReader("WRONG\n"));

            Assert.That(Program.RunMain(["--replay-worker", "missing.bin"], "dotnet"), Is.EqualTo(2));
        }
        finally
        {
            Console.SetIn(originalInput);
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, previous);
        }
    }

    [Test, NonParallelizable]
    public void ReplayWorkerRejectsAnIncorrectStartMarker()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-worker-marker-{Guid.NewGuid():N}");
        string? previous = Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable);
        TextReader originalInput = Console.In;
        try
        {
            string inputPath = FuzzCorpus.Materialize(outputDirectory)
                .Single(path => path.EndsWith("ofs-delta-copy-base.bin", StringComparison.Ordinal));
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, "1");
            Console.SetIn(new StringReader(
                $"{BoundedReplayRunner.WorkerWarmupMarker}{Environment.NewLine}WRONG{Environment.NewLine}"));

            Assert.That(Program.RunMain(["--replay-worker", inputPath], "dotnet"), Is.EqualTo(2));
        }
        finally
        {
            Console.SetIn(originalInput);
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test, NonParallelizable]
    public void ReplayWorkerCompletesTheWarmupAndCandidateHandshake()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-git-fuzz-worker-{Guid.NewGuid():N}");
        string? previous = Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable);
        TextReader originalInput = Console.In;
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();
        try
        {
            string inputPath = FuzzCorpus.Materialize(outputDirectory)
                .Single(path => path.EndsWith("ofs-delta-copy-base.bin", StringComparison.Ordinal));
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, "1");
            Console.SetIn(new StringReader(
                $"{BoundedReplayRunner.WorkerWarmupMarker}{Environment.NewLine}"
                + $"{BoundedReplayRunner.WorkerStartMarker}{Environment.NewLine}"));
            Console.SetOut(output);
            Console.SetError(error);

            int exitCode = Program.RunMain(["--replay-worker", inputPath], "dotnet");

            Assert.That(exitCode, Is.Zero);
            Assert.That(output.ToString(), Does.Contain(BoundedReplayRunner.WorkerReadyMarker));
            Assert.That(output.ToString(), Does.Contain(BoundedReplayRunner.WorkerCaseReadyMarker));
            Assert.That(output.ToString(), Does.Contain("Canonical"));
            Assert.That(error.ToString(), Is.Empty);
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable, previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void ProgramUsageRejectsUnknownArguments()
    {
        Assert.That(Program.RunMain(["--unknown", "value"], "dotnet"), Is.EqualTo(2));
    }

    private static bool DockerIsUsable()
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            })!;
            return process.WaitForExit(5_000) && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static BoundedReplayRunner.ReplayCommand ShellCommand(string script)
        => OperatingSystem.IsWindows()
            ? new("cmd.exe", ["/c", script], UsesWindowsJobObject: true)
            : new("/bin/sh", ["-c", script], UsesWindowsJobObject: false);
}
