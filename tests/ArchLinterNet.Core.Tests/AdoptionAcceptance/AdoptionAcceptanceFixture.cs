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

    public void Build(string? configuration = null, string? targetFramework = null)
    {
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

        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Fixture '{Id}' failed to build.{Environment.NewLine}{output}{Environment.NewLine}{error}");
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
}
