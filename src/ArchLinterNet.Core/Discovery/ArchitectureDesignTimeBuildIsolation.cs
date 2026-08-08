namespace ArchLinterNet.Core.Discovery;

internal sealed class ArchitectureDesignTimeBuildIsolation : IDisposable
{
    private readonly string _directory;

    private ArchitectureDesignTimeBuildIsolation(string directory)
    {
        _directory = directory;
        IntermediateOutputPath = Path.Combine(directory, "obj") + Path.DirectorySeparatorChar;
    }

    public string IntermediateOutputPath { get; }

    public static ArchitectureDesignTimeBuildIsolation Create()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-design-time-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return new ArchitectureDesignTimeBuildIsolation(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
