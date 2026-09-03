using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CheckpointBProcessRunnerTests
{
    [Test]
    [CancelAfter(10_000)]
    public void CancellationKillsTheDescendantProcessTree()
    {
        string root = Path.Combine(Path.GetTempPath(), $"checkpoint-b-process-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string childPidPath = Path.Combine(root, "child.pid");
        using var cancellation = new CancellationTokenSource();
        Task<Exception?>? runner = null;
        bool cleanupTimedOut = false;
        try
        {
            ProcessStartInfo startInfo = CreateProcessTree(childPidPath);
            runner = Task.Run(() =>
            {
                try
                {
                    _ = CheckpointBReleaseGateTests.Run(startInfo, cancellation.Token);
                    return null;
                }
                catch (Exception error)
                {
                    return error;
                }
            });

            Assert.That(SpinWait.SpinUntil(
                    () => File.Exists(childPidPath) && int.TryParse(File.ReadAllText(childPidPath).Trim(), out _),
                    TimeSpan.FromSeconds(5)),
                Is.True, "The probe did not publish its descendant process id.");
            int childPid = int.Parse(File.ReadAllText(childPidPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(IsProcessAlive(childPid), Is.True, "The descendant must be alive before cancellation.");

            cancellation.Cancel();
            Assert.That(runner.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "The bounded process runner did not return after cancellation.");
            Assert.That(runner.Result, Is.InstanceOf<OperationCanceledException>(),
                "The bounded process runner must propagate cancellation after killing the tree.");
            Assert.That(SpinWait.SpinUntil(() => !IsProcessAlive(childPid), TimeSpan.FromSeconds(5)), Is.True,
                "Cancellation must terminate descendants, not only the direct child process.");
        }
        finally
        {
            cancellation.Cancel();
            if (runner is not null && !runner.IsCompleted && !runner.Wait(TimeSpan.FromSeconds(5)))
            {
                cleanupTimedOut = true;
            }

            DeleteDirectoryEventually(root);
        }

        if (cleanupTimedOut)
        {
            throw new TimeoutException("The process-tree probe did not stop during test cleanup.");
        }
    }

    [Test]
    [CancelAfter(10_000)]
    public void WindowsRootExitWithInheritedStreamHandleFailsBoundedlyAndCleansUpDescendant()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The inherited redirected-handle regression requires Windows job objects.");
        }

        string root = Path.Combine(Path.GetTempPath(), $"checkpoint-b-process-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string childPidPath = Path.Combine(root, "child.pid");
        ProcessStartInfo startInfo = CreateRootExitWithInheritedHandle(childPidPath);
        Stopwatch elapsed = Stopwatch.StartNew();

        try
        {
            TimeoutException? failure = Assert.Throws<TimeoutException>(() =>
                CheckpointBReleaseGateTests.Run(startInfo, TestContext.CurrentContext.CancellationToken));

            Assert.That(elapsed.Elapsed, Is.LessThan(
                    CheckpointBProcessRunner.PostExitDrainTimeout
                    + CheckpointBProcessRunner.CleanupTimeout
                    + TimeSpan.FromSeconds(2)),
                "The retained-handle probe must fail within the configured runner bounds.");
            Assert.That(failure!.Message, Does.Contain("post-exit stream drain"));
            Assert.That(failure.Message, Does.Contain("Command:"));
            Assert.That(failure.Message, Does.Contain("root PID:"));
            Assert.That(failure.Message, Does.Contain("elapsed duration:"));
            Assert.That(failure.Message, Does.Contain("stdout tail"));
            Assert.That(failure.Message, Does.Contain("stderr tail"));

            Assert.That(SpinWait.SpinUntil(
                    () => File.Exists(childPidPath) && int.TryParse(File.ReadAllText(childPidPath).Trim(), out _),
                    TimeSpan.FromSeconds(2)),
                Is.True,
                "The inherited-handle probe did not publish its descendant process id.");
            int childPid = int.Parse(File.ReadAllText(childPidPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(SpinWait.SpinUntil(() => !IsProcessAlive(childPid), TimeSpan.FromSeconds(2)), Is.True,
                "Closing the Windows job scope must terminate a descendant after the root exits.");
        }
        finally
        {
            DeleteDirectoryEventually(root);
        }
    }

    [Test]
    [CancelAfter(90_000)]
    public void ImmediateRootExitDoesNotRaceProcessAttachment()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("This regression targets the Windows CreateProcessW handle-attachment race.");
        }

        // A root that exits the instant it starts is the worst case for the window between
        // CreateProcessW returning and this runner attaching a managed Process by PID: run it many
        // times to make a reintroduced race (attaching by PID after the process could already have
        // exited and its PID been reused) show up as an intermittent failure rather than a rare one.
        for (int iteration = 0; iteration < 150; iteration++)
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("exit 0");

            CheckpointBReleaseGateTests.CommandResult result =
                CheckpointBReleaseGateTests.Run(startInfo, TestContext.CurrentContext.CancellationToken);
            Assert.That(result.ExitCode, Is.EqualTo(0),
                $"Iteration {iteration} did not observe a clean exit code from an immediately-exiting root.");
        }
    }

    [Test]
    [CancelAfter(10_000)]
    public void StreamDecodeFaultDuringDrainPreservesOriginalExceptionAndTerminatesDescendant()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The inherited redirected-handle regression requires Windows job objects.");
        }

        string root = Path.Combine(Path.GetTempPath(), $"checkpoint-b-process-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string childPidPath = Path.Combine(root, "child.pid");

        // Reuses the same inherited-handle probe as the timeout regression above; the fault comes
        // not from the probe's output but from decoding it with an encoding rigged to always throw,
        // so the descendant's ordinary "descendant-output" write is enough to trigger it. Only
        // stdout is rigged: stderr keeps the default encoding and legitimately stays blocked for the
        // whole drain window, since the descendant holds it open (the same shape as the timeout
        // regression above). Task.WhenAll(stdout, stderr) alone would not complete until stderr also
        // finishes, masking stdout's fault behind a drain timeout; the runner must observe the fault
        // on the single faulted stream without waiting for the other.
        var startInfo = new ProcessStartInfo(ProcessTreeProbePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = AlwaysThrowingDecodeEncoding.Instance,
        };
        startInfo.ArgumentList.Add("root");
        startInfo.ArgumentList.Add(childPidPath);
        startInfo.ArgumentList.Add("30");

        try
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            // This is a genuine fault raised while decoding the redirected stdout stream, not the
            // runner's own timeout or a cancellation: it must surface unchanged, proving cleanup
            // never replaces the original exception with one of its own (e.g. a cleanup-phase
            // TimeoutException), and that a fault mid-drain still runs cleanup at all.
            Assert.Throws<InvalidDataException>(() =>
                CheckpointBReleaseGateTests.Run(startInfo, TestContext.CurrentContext.CancellationToken));

            Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)),
                "A fault on one stream must surface as soon as it happens, not be masked behind " +
                "Task.WhenAll waiting for the other (still legitimately blocked) stream to finish " +
                "and the drain bound to elapse.");

            Assert.That(SpinWait.SpinUntil(
                    () => File.Exists(childPidPath) && int.TryParse(File.ReadAllText(childPidPath).Trim(), out _),
                    TimeSpan.FromSeconds(2)),
                Is.True,
                "The inherited-handle probe did not publish its descendant process id.");
            int childPid = int.Parse(File.ReadAllText(childPidPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(SpinWait.SpinUntil(() => !IsProcessAlive(childPid), TimeSpan.FromSeconds(2)), Is.True,
                "A fault while decoding a redirected stream must still trigger bounded cleanup that " +
                "terminates the descendant, not leave it running because the exceptional path skipped " +
                "cleanup.");
        }
        finally
        {
            DeleteDirectoryEventually(root);
        }
    }

    [Test]
    [CancelAfter(15_000)]
    public void StreamDecodeFaultWhileProcessStillRunningSurfacesWithoutProcessCompletionBound()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The inherited redirected-handle regression requires Windows job objects.");
        }

        // The root here writes a line and then sleeps well past this test's own bound without
        // exiting, so process completion never wins its own race. A fix that only inspected the
        // stream tasks after process completion (or after the drain phase started) would have to
        // wait out ProcessCompletionTimeout (2 minutes) — far longer than this test allows — instead
        // of observing the fault as soon as it happens.
        var startInfo = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = AlwaysThrowingDecodeEncoding.Instance,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            "[Console]::Out.WriteLine('trigger'); [Console]::Out.Flush(); Start-Sleep -Seconds 60");

        Stopwatch elapsed = Stopwatch.StartNew();

        Assert.Throws<InvalidDataException>(() =>
            CheckpointBReleaseGateTests.Run(startInfo, TestContext.CurrentContext.CancellationToken));

        Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
            "A genuine stream-decode fault must surface as soon as it happens even while the " +
            "process itself is still running, not only once process completion or drain bounds out.");
    }

    /// <summary>
    /// A minimal <see cref="Encoding"/> whose decoding side always throws, used to deterministically
    /// inject a fault while a redirected stream is being decoded, independent of any real invalid
    /// byte sequence or Decoder buffering/EOF timing.
    /// </summary>
    private sealed class AlwaysThrowingDecodeEncoding : Encoding
    {
        internal static readonly AlwaysThrowingDecodeEncoding Instance = new();

        public override int GetByteCount(char[] chars, int index, int count) => 0;

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) => 0;

        public override int GetCharCount(byte[] bytes, int index, int count) =>
            throw new InvalidDataException("Injected stream-decode fault for testing.");

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) =>
            throw new InvalidDataException("Injected stream-decode fault for testing.");

        public override int GetMaxByteCount(int charCount) => charCount;

        public override int GetMaxCharCount(int byteCount) => byteCount;
    }

    [Test]
    [CancelAfter(5_000)]
    public async Task WaitBestEffortAsyncSwallowsAFaultedTask()
    {
        Task faulted = Task.FromException(new InvalidOperationException("boom"));

        // No exception escaping this call is the assertion: a fault on the task being cleaned up
        // must never replace the exception the caller is already propagating.
        await CheckpointBProcessRunner.WaitBestEffortAsync(faulted, TimeSpan.FromSeconds(1));
    }

    [Test]
    [CancelAfter(5_000)]
    public async Task WaitBestEffortAsyncSwallowsATimeout()
    {
        var neverCompletes = new TaskCompletionSource();
        Stopwatch elapsed = Stopwatch.StartNew();

        await CheckpointBProcessRunner.WaitBestEffortAsync(neverCompletes.Task, TimeSpan.FromMilliseconds(200));

        Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)),
            "A best-effort cleanup wait must return once its own timeout elapses, not hang indefinitely.");
    }

    private static ProcessStartInfo CreateProcessTree(string childPidPath)
    {
        if (OperatingSystem.IsWindows())
        {
            string escapedPath = childPidPath.Replace("'", "''", StringComparison.Ordinal);
            var startInfo = new ProcessStartInfo("pwsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                "$child = Start-Process pwsh -ArgumentList '-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 30' -PassThru; "
                + $"[System.IO.File]::WriteAllText('{escapedPath}', [string]$child.Id); Wait-Process -Id $child.Id");
            return startInfo;
        }

        var shell = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        shell.ArgumentList.Add("-c");
        shell.ArgumentList.Add("sleep 30 & child=$!; printf '%s' \"$child\" > \"$1\"; wait \"$child\"");
        shell.ArgumentList.Add("checkpoint-b-process-runner");
        shell.ArgumentList.Add(childPidPath);
        return shell;
    }

    /// <summary>
    /// Root exits immediately after starting a descendant that inherits the root's own
    /// redirected stdout/stderr pipe handles and then sleeps: the descendant, not the root,
    /// keeps the write end of those pipes open past the post-exit drain bound. This is the
    /// production failure mode being regression-tested, reproduced deterministically via a
    /// dedicated native helper instead of a shell (`cmd.exe /c start`) whose own exit-signaling
    /// timing is not guaranteed on every Windows image.
    /// </summary>
    private static ProcessStartInfo CreateRootExitWithInheritedHandle(string childPidPath)
    {
        var startInfo = new ProcessStartInfo(ProcessTreeProbePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("root");
        startInfo.ArgumentList.Add(childPidPath);
        startInfo.ArgumentList.Add("30");
        return startInfo;
    }

    private static string ProcessTreeProbePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "ArchLinterNet.ProcessTreeProbe.exe");

    private static void DeleteDirectoryEventually(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        Exception? lastError = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException error)
            {
                lastError = error;
            }
            catch (UnauthorizedAccessException error)
            {
                lastError = error;
            }

            Thread.Sleep(25);
        }

        throw new IOException($"Timed out deleting process-runner probe directory '{root}'.", lastError);
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
