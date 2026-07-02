namespace ArchLinterNet.Core.IO;

public interface IArchitectureEnvironment
{
    string? GetEnvironmentVariable(string name);

    string BaseDirectory { get; }
}

public sealed class ArchitectureEnvironment : IArchitectureEnvironment
{
    public static readonly ArchitectureEnvironment Real = new();

    public string? GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    public string BaseDirectory => AppContext.BaseDirectory;
}
