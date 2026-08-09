namespace ArchLinterNet.Core.Discovery;

internal sealed class ArchitectureDesignTimeBuildIsolation : IDisposable
{
    private readonly string _projectIntermediateDirectory;

    private ArchitectureDesignTimeBuildIsolation(string projectIntermediateDirectory, string cleanFileName)
    {
        _projectIntermediateDirectory = projectIntermediateDirectory;
        CleanFileName = cleanFileName;
    }

    public string CleanFileName { get; }

    public static ArchitectureDesignTimeBuildIsolation Create(string projectAbsolutePath)
    {
        string projectDirectory = Path.GetDirectoryName(projectAbsolutePath)
            ?? throw new ArgumentException("The project path must include a directory.", nameof(projectAbsolutePath));
        string cleanFileName = $"ArchLinterNet.DesignTime.{Guid.NewGuid():N}.FileListAbsolute.txt";
        return new ArchitectureDesignTimeBuildIsolation(Path.Combine(projectDirectory, "obj"), cleanFileName);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_projectIntermediateDirectory))
        {
            return;
        }

        foreach (string cleanFilePath in Directory.GetFiles(
                     _projectIntermediateDirectory, CleanFileName, SearchOption.AllDirectories))
        {
            File.Delete(cleanFilePath);
        }
    }
}
