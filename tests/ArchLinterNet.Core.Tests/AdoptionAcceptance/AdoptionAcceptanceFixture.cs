using System.Diagnostics;

namespace ArchLinterNet.Core.Tests;

internal sealed class AdoptionAcceptanceFixture : IDisposable
{
    private AdoptionAcceptanceFixture(string id, string root)
    {
        Id = id;
        Root = root;
    }

    public string Id { get; }

    public string Root { get; }

    public string PolicyPath => Path.Combine(Root, "dependencies.arch.yml");

    public IReadOnlyList<string> ProjectPaths => Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories);

    public IReadOnlyList<string> SourcePaths => Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories);

    public static AdoptionAcceptanceFixture Create(string id)
    {
        string source = Path.Combine(
            Path.GetDirectoryName(CheckpointAAdoptionAcceptanceTests.ManifestPath())!,
            "Fixtures",
            id);
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException($"Unknown adoption fixture root '{id}'.");
        }

        string temporaryRoot = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && temporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
        {
            temporaryRoot = $"/private{temporaryRoot}";
        }

        string destination = Path.Combine(
            temporaryRoot,
            $"arch-linter-adoption-{id}-{Guid.NewGuid():N}");
        CopyDirectory(source, destination);
        return new AdoptionAcceptanceFixture(id, destination);
    }

    public void Build(
        string? configuration = null,
        string? targetFramework = null,
        CancellationToken cancellationToken = default,
        int timeoutMilliseconds = 300_000)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string buildTarget = Directory.GetFiles(Root, "*.slnx", SearchOption.TopDirectoryOnly).SingleOrDefault()
            ?? ProjectPaths[0];
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(buildTarget);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("--maxcpucount:1");
        if (!string.IsNullOrWhiteSpace(configuration))
        {
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(configuration);
        }

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            startInfo.ArgumentList.Add("--framework");
            startInfo.ArgumentList.Add(targetFramework);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromMilliseconds(timeoutMilliseconds));
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        try
        {
            process.WaitForExitAsync(linkedSource.Token).GetAwaiter().GetResult();
            string output = outputTask.WaitAsync(linkedSource.Token).GetAwaiter().GetResult();
            string error = errorTask.WaitAsync(linkedSource.Token).GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Fixture '{Id}' failed to build.{Environment.NewLine}{output}{Environment.NewLine}{error}");
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            AwaitProcessCleanup(process, outputTask, errorTask);
            throw new TimeoutException($"Fixture '{Id}' build exceeded {timeoutMilliseconds}ms.");
        }
        catch
        {
            TryKillProcessTree(process);
            AwaitProcessCleanup(process, outputTask, errorTask);
            throw;
        }
    }

    public long AddLargeEmbeddedResource(string fileName, long byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteCount);

        string resourcePath = Path.Combine(Root, fileName);
        using (FileStream stream = File.Create(resourcePath))
        {
            stream.SetLength(byteCount);
        }

        string projectPath = ProjectPaths.Single();
        string project = File.ReadAllText(projectPath);
        File.WriteAllText(projectPath, project.Replace("</Project>", $"  <ItemGroup><EmbeddedResource Include=\"{fileName}\" /></ItemGroup>{Environment.NewLine}</Project>", StringComparison.Ordinal));
        return byteCount;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name is "bin" or "obj")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, name));
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
            // The process exited between cancellation and Kill; cleanup is already complete.
        }
    }

    private static void AwaitProcessCleanup(Process process, params Task<string>[] outputTasks)
    {
        try
        {
            process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Task.WhenAll(outputTasks).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch
        {
            // The original cancellation, timeout, or process error remains authoritative.
        }
    }
}
