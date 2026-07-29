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

        string destination = Path.Combine(
            Path.GetTempPath(),
            $"arch-linter-adoption-{id}-{Guid.NewGuid():N}");
        CopyDirectory(source, destination);
        return new AdoptionAcceptanceFixture(id, destination);
    }

    public void Build()
    {
        string buildTarget = Directory.GetFiles(Root, "*.slnx", SearchOption.TopDirectoryOnly).SingleOrDefault()
            ?? ProjectPaths.First();
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
