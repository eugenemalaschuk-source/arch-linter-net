using System.Diagnostics;
using System.Text;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

// Owns the structured dotnet child-process boundary for --ensure-built, including graph build
// orchestration, concurrent output draining, cancellation cleanup, and bounded process teardown.
internal static class BuildStateRuntimeBuildProcessExecutor
{
    private const string ContractName = "build-state-preflight";
    private const int ProcessPollIntervalMs = 100;
    private const int ProcessExitAfterKillTimeoutMs = 5_000;
    private const int OutputDrainTimeoutMs = 1_000;

    internal static BuildStatePreflightDiagnostic? InvokeGraphBuild(BuildStatePreflightRequest request)
    {
        bool buildsRuntimeSpecificOutput = request.RequestedRuntimeIdentifier != null;
        string buildTargetPath = buildsRuntimeSpecificOutput
            ? BuildStateRuntimeGraphBuildProjectFactory.WriteTemporaryRuntimeGraphBuildProject(request)
            : BuildStateRuntimeBuildPreparation.WriteTemporaryGraphSolution(request);
        try
        {
            // Restore once up front, before building with --no-restore. --disable-parallel avoids
            // NuGet restore races when a shared project is both a solution entry and a reference.
            if (!request.NoRestore)
            {
                List<string> restoreArguments =
                    new() { "restore", buildTargetPath, "--nologo", "-m:1", "--disable-parallel" };
                BuildStatePreflightDiagnostic? restoreFailure = RunDotnetCommand(
                    request, restoreArguments, "restore", BuildStatePreflightState.RestoreFailed);
                if (restoreFailure != null)
                {
                    return restoreFailure;
                }
            }

            // A shared project can be both a solution entry and a ProjectReference. Keep MSBuild
            // single-node here to avoid concurrent writes to its intermediate files.
            List<string> arguments = new() { "build", buildTargetPath, "--nologo", "--no-restore", "-m:1" };
            if (!buildsRuntimeSpecificOutput && request.RequestedConfiguration != null)
            {
                arguments.Add("-c");
                arguments.Add(request.RequestedConfiguration);
            }

            if (!buildsRuntimeSpecificOutput)
            {
                AddFrameworkArgument(arguments, request.RequestedTargetFramework);
                if (request.RequestedPlatform != null)
                {
                    arguments.Add($"-p:Platform={request.RequestedPlatform}");
                }
            }

            return RunDotnetCommand(request, arguments, "build", BuildStatePreflightState.BuildFailed);
        }
        finally
        {
            File.Delete(buildTargetPath);
        }
    }

    // This seam is intentionally limited to ProcessStartInfo construction: tests can prove that
    // untrusted paths remain one structured ArgumentList entry without starting a child process.
    internal static ProcessStartInfo CreateDotnetProcessStartInfo(
        BuildStatePreflightRequest request, IReadOnlyCollection<string> arguments)
    {
        ProcessStartInfo startInfo = new(ResolveDotnetExecutablePath())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = request.RepositoryRoot,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string ResolveDotnetExecutablePath()
    {
        string executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
        if (dotnetRoot != null)
        {
            string candidate = Path.Combine(dotnetRoot, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        foreach (string directory in (pathVariable ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }

    private static void AddFrameworkArgument(List<string> arguments, string? requestedTargetFramework)
    {
        if (requestedTargetFramework != null)
        {
            arguments.Add("-f");
            arguments.Add(requestedTargetFramework);
        }
    }

    private static BuildStatePreflightDiagnostic? RunDotnetCommand(
        BuildStatePreflightRequest request, List<string> arguments, string commandLabel, BuildStatePreflightState failureState)
    {
        ProcessStartInfo startInfo = CreateDotnetProcessStartInfo(request, arguments);
        using Process process = new() { StartInfo = startInfo };
        StringBuilder stdOut = new();
        StringBuilder stdErr = new();
        object outputGate = new();
        TaskCompletionSource outputCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource errorCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                outputCompleted.TrySetResult();
            }
            else
            {
                lock (outputGate)
                {
                    stdOut.AppendLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                errorCompleted.TrySetResult();
            }
            else
            {
                lock (outputGate)
                {
                    stdErr.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        WaitForExitOrCancellation(process, outputCompleted.Task, errorCompleted.Task, request.CancellationToken);

        if (process.ExitCode == 0)
        {
            return null;
        }

        string combinedOutput;
        lock (outputGate)
        {
            combinedOutput = (stdOut.ToString() + stdErr).Trim();
        }

        string actualCommand = "dotnet " + string.Join(' ', arguments.Select(QuoteIfNeeded));
        return new BuildStatePreflightDiagnostic(
            ContractName,
            request.RepositoryRoot,
            failureState,
            new BuildStatePreflightEvidence(
                request.RepositoryRoot,
                string.Join(", ", request.ProjectDiscovery.DiscoveredProjects.Select(p => p.AssemblyName)),
                BuildCommand: actualCommand,
                Detail: $"`dotnet {commandLabel}` failed with exit code {process.ExitCode}: {combinedOutput}"));
    }

    private static string QuoteIfNeeded(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;

    private static void WaitForExitOrCancellation(
        Process process,
        Task outputCompleted,
        Task errorCompleted,
        CancellationToken cancellationToken)
    {
        WaitForExitOrCancellationCore(process.WaitForExit, () => TryKillProcessTree(process), process.Id, cancellationToken);
        Task.WaitAll([outputCompleted, errorCompleted], OutputDrainTimeoutMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    // Fake delegates keep the kill-then-bounded-wait timeout branch deterministic in tests.
    internal static void WaitForExitOrCancellationCore(
        Func<int, bool> waitForExit, Action killProcessTree, int processId, CancellationToken cancellationToken)
    {
        while (!waitForExit(ProcessPollIntervalMs))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                killProcessTree();
                if (!waitForExit(ProcessExitAfterKillTimeoutMs))
                {
                    throw new BuildStateProcessCleanupTimedOutException(processId, ProcessExitAfterKillTimeoutMs, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the poll check and this call — benign race.
        }
    }
}
