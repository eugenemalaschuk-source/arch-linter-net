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
