using System.Diagnostics;
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
